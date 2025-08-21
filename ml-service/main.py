# main.py
import os
import json
import time
import pandas as pd
from fastapi import FastAPI, HTTPException, Request
from fastapi.responses import JSONResponse, StreamingResponse
from trainer import train, ensure_timestamp
import joblib

app = FastAPI()
DATA_PATH = os.environ.get("DATA_PATH", "/app/data/upload.csv")
MODEL_PATH = os.environ.get("MODEL_PATH", "/app/data/model.joblib")

@app.get("/health")
async def health():
    return {"status": "ok"}

# Preprocess Bosch dataset into /app/data/upload.csv
@app.post("/preprocess-bosch")
async def preprocess_bosch(req: Request):
    payload = await req.json()
    # Expect paths mounted at /app/data
    numeric_path = payload.get("numericPath", "/app/data/train_numeric.csv")
    labels_path = payload.get("labelsPath", "/app/data/train_labels.csv")
    target_rows = int(payload.get("targetRows", 100000))
    pos_fraction = float(payload.get("positiveFraction", 0.2))

    try:
        import numpy as np
        import pandas as pd
    except Exception:
        raise HTTPException(status_code=500, detail="pandas/numpy not available")

    # Peek to see if Response already present
    try:
        head = pd.read_csv(numeric_path, nrows=5)
    except Exception as e:
        raise HTTPException(status_code=400, detail=f"Cannot read numeric file: {e}")
    response_in_numeric = 'Response' in head.columns
    labels_df = None
    if not response_in_numeric:
        try:
            labels_df = pd.read_csv(labels_path)
        except Exception as e:
            raise HTTPException(status_code=400, detail=f"Cannot read labels file: {e}")
        if 'Response' not in labels_df.columns:
            raise HTTPException(status_code=400, detail="labels file missing Response column")

    chunks = []
    pos_needed = int(target_rows * pos_fraction)
    neg_needed = target_rows - pos_needed
    pos_acc, neg_acc = 0, 0

    try:
        for chunk in pd.read_csv(numeric_path, chunksize=50000, low_memory=False):
            if not response_in_numeric:
                chunk = chunk.merge(labels_df, on='Id', how='left')
            if 'Response' not in chunk.columns:
                continue
            numeric_cols = chunk.select_dtypes(include=['number']).columns.tolist()
            if 'Response' not in numeric_cols:
                numeric_cols.append('Response')
            work = chunk[numeric_cols].copy()
            work['Response'] = work['Response'].fillna(0).astype(int)
            pos = work[work['Response'] == 1]
            neg = work[work['Response'] == 0]

            take_pos = min(len(pos), max(0, pos_needed - pos_acc))
            take_neg = min(len(neg), max(0, neg_needed - neg_acc))
            if take_pos > 0:
                chunks.append(pos.sample(n=take_pos, random_state=42))
                pos_acc += take_pos
            if take_neg > 0:
                chunks.append(neg.sample(n=take_neg, random_state=42))
                neg_acc += take_neg
            if pos_acc + neg_acc >= target_rows:
                break
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Error during chunking: {e}")

    if not chunks:
        raise HTTPException(status_code=400, detail="No rows collected - check file paths")

    df = pd.concat(chunks, ignore_index=True)
    # Add synthetic timestamps
    start = pd.to_datetime("2021-01-01 00:00:00")
    df['synthetic_timestamp'] = [start + pd.Timedelta(seconds=i) for i in range(len(df))]
    out_path = DATA_PATH  # /app/data/upload.csv
    try:
        df.to_csv(out_path, index=False)
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to write upload.csv: {e}")

    return JSONResponse({
        "rows": int(len(df)),
        "columns": int(len(df.columns)),
        "start": str(df['synthetic_timestamp'].min()),
        "end": str(df['synthetic_timestamp'].max()),
        "path": out_path
    })

@app.post("/train-model")
async def train_model(req: Request):
    """
    Expects JSON:
    {
      "trainStart": "2021-01-01T00:00:00",
      "trainEnd":   "...",
      "testStart":  "...",
      "testEnd":    "..."
    }
    """
    payload = await req.json()
    for key in ("trainStart", "trainEnd", "testStart", "testEnd"):
        if key not in payload:
            raise HTTPException(status_code=400, detail=f"{key} required")
    if not os.path.exists(DATA_PATH):
        raise HTTPException(status_code=400, detail="Dataset not found on server")
    df = pd.read_csv(DATA_PATH)
    try:
        result = train(df, payload["trainStart"], payload["trainEnd"], payload["testStart"], payload["testEnd"], model_path=MODEL_PATH)
    except ValueError as e:
        raise HTTPException(status_code=400, detail=str(e))
    return JSONResponse(result)

@app.get("/simulate")
async def simulate(start: str, end: str):
    """
    Streams Server-Sent Events (SSE). Query params: start, end (ISO timestamps)
    """
    if not os.path.exists(DATA_PATH):
        raise HTTPException(status_code=400, detail="Dataset not found")
    if not os.path.exists(MODEL_PATH):
        raise HTTPException(status_code=400, detail="Trained model not found; run /train-model first")

    df = pd.read_csv(DATA_PATH)
    df = ensure_timestamp(df)
    df['synthetic_timestamp'] = pd.to_datetime(df['synthetic_timestamp'])
    mask = (df['synthetic_timestamp'] >= pd.to_datetime(start)) & (df['synthetic_timestamp'] <= pd.to_datetime(end))
    subset = df[mask].copy()
    if subset.empty:
        raise HTTPException(status_code=400, detail="No rows in simulation window")

    # load model
    model = joblib.load(MODEL_PATH)
    numeric_cols = subset.select_dtypes(include=['number']).columns.tolist()
    # ensure Response not used as input
    if 'Response' in numeric_cols:
        numeric_cols.remove('Response')

    def gen():
        for _, row in subset.iterrows():
            X = row[numeric_cols].to_frame().T.fillna(0)
            try:
                if hasattr(model, "predict_proba"):
                    proba = float(model.predict_proba(X)[:, 1][0])
                    pred = int(proba >= 0.5)
                else:
                    # lightgbm booster
                    proba = float(model.predict(X)[0])
                    pred = int(proba >= 0.5)
            except Exception:
                proba = 0.0
                pred = 0
            out = {
                "timestamp": str(row['synthetic_timestamp']),
                "id": int(row.get('ID', _)),
                "prediction": pred,
                "confidence": proba
            }
            # copy a few named sensors if present
            for field in ("Temperature","Pressure","Humidity"):
                if field in row:
                    try:
                        out[field.lower()] = float(row[field])
                    except Exception:
                        out[field.lower()] = None
            yield f"data: {json.dumps(out)}\n\n"
            time.sleep(1)
    return StreamingResponse(gen(), media_type="text/event-stream")

#changes start from here
# Non-streaming JSON simulation endpoint to match current frontend expectations
@app.post("/simulate-json")
async def simulate_json(req: Request):
    payload = await req.json()
    start = payload.get("simulationStart")
    end = payload.get("simulationEnd")
    if not start or not end:
        raise HTTPException(status_code=400, detail="simulationStart and simulationEnd required")

    if not os.path.exists(DATA_PATH):
        raise HTTPException(status_code=400, detail="Dataset not found")
    if not os.path.exists(MODEL_PATH):
        raise HTTPException(status_code=400, detail="Trained model not found; run /train-model first")

    df = pd.read_csv(DATA_PATH)
    df = ensure_timestamp(df)
    df['synthetic_timestamp'] = pd.to_datetime(df['synthetic_timestamp'])
    mask = (df['synthetic_timestamp'] >= pd.to_datetime(start)) & (df['synthetic_timestamp'] <= pd.to_datetime(end))
    subset = df[mask].copy()
    if subset.empty:
        raise HTTPException(status_code=400, detail="No rows in simulation window")

    model = joblib.load(MODEL_PATH)
    numeric_cols = subset.select_dtypes(include=['number']).columns.tolist()
    if 'Response' in numeric_cols:
        numeric_cols.remove('Response')

    records = []
    pass_count = 0
    confidences = []
    for idx, row in subset.iterrows():
        X = row[numeric_cols].to_frame().T.fillna(0)
        try:
            if hasattr(model, "predict_proba"):
                proba = float(model.predict_proba(X)[:, 1][0])
                pred = int(proba >= 0.5)
            else:
                proba = float(model.predict(X)[0])
                pred = int(proba >= 0.5)
        except Exception:
            proba = 0.0
            pred = 0
        confidences.append(proba)
        pass_count += 1 if pred == 1 else 0
        out = {
            "timestamp": str(row['synthetic_timestamp']),
            "id": int(row.get('ID', idx)),
            "prediction": "Pass" if pred == 1 else "Fail",
            "confidence": proba * 100.0
        }
        for field in ("Temperature","Pressure","Humidity"):
            if field in row:
                try:
                    out[field.lower()] = float(row[field])
                except Exception:
                    out[field.lower()] = None
        records.append(out)

    total = len(records)
    avg_conf = float(sum(confidences) / total * 100.0) if total else 0.0
    summary = {
        "totalPredictions": total,
        "passCount": pass_count,
        "failCount": total - pass_count,
        "averageConfidence": avg_conf
    }
    return JSONResponse({"records": records, "summary": summary})