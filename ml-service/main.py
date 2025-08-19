from fastapi import FastAPI, HTTPException, Request
from fastapi.responses import JSONResponse, StreamingResponse
import pandas as pd
import os, io, time, json
from trainer import train_and_eval, load_model, predict_row
from datetime import datetime

app = FastAPI()
DATA_PATH = os.environ.get("DATA_DIR", "/data")
PROCESSED_CSV = os.path.join(DATA_PATH, "processed.csv")
MODEL_PATH = os.path.join("models", "model.joblib")

@app.post("/train")
async def train_endpoint(payload: dict):
    # expected payload keys: trainStart, trainEnd, testStart, testEnd
    if not os.path.exists(PROCESSED_CSV):
        raise HTTPException(400, "Processed dataset not found on ML service.")
    metrics = train_and_eval(PROCESSED_CSV, payload)
    return JSONResponse(content=metrics)

def sse_event(data: dict):
    return f"data: {json.dumps(data)}\n\n"

@app.get("/simulate")
async def simulate(simStart: str, simEnd: str):
    if not os.path.exists(PROCESSED_CSV):
        raise HTTPException(400, "Processed dataset not found.")
    df = pd.read_csv(PROCESSED_CSV, parse_dates=['synthetic_timestamp'])
    mask = (df['synthetic_timestamp'] >= simStart) & (df['synthetic_timestamp'] <= simEnd)
    df_slice = df.loc[mask].sort_values('synthetic_timestamp').reset_index(drop=True)
    model = load_model(MODEL_PATH)
    total = len(df_slice)
    pass_count = 0
    fail_count = 0
    conf_sum = 0.0

    def generator():
        nonlocal pass_count, fail_count, conf_sum
        for idx, row in df_slice.iterrows():
            # predict
            pred, conf = predict_row(model, row)
            # create simple sensor proxies (if real columns missing)
            temperature = float(row.get('S1', 20.0)) % 100
            pressure = float(row.get('S2', 1000.0)) % 1200
            humidity = float(row.get('S3', 50.0)) % 100
            rec = {
                "timestamp": str(row['synthetic_timestamp']),
                "sampleId": int(row.get('Id', idx)),
                "prediction": "Pass" if pred==1 else "Fail",
                "confidence": round(float(conf), 3),
                "temperature": round(float(temperature),2),
                "pressure": round(float(pressure),2),
                "humidity": round(float(humidity),2)
            }
            if pred==1: pass_count += 1
            else: fail_count += 1
            conf_sum += float(conf)
            yield sse_event(rec)
            time.sleep(1)  # emit one row per second
        yield sse_event({"summary": {"total": total, "pass": pass_count, "fail": fail_count,
                                     "avg_confidence": round(conf_sum/max(1,total),3)}})
    return StreamingResponse(generator(), media_type="text/event-stream")
