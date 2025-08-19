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
