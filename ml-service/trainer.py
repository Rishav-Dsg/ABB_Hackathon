# trainer.py
import os
import io
import base64
import pandas as pd
import numpy as np
from sklearn.ensemble import RandomForestClassifier
from sklearn.metrics import accuracy_score, precision_score, recall_score, f1_score, confusion_matrix
import joblib
import matplotlib.pyplot as plt

# Try to import LightGBM
try:
    import lightgbm as lgb
    HAS_LGB = True
except Exception:
    HAS_LGB = False

MODEL_PATH_DEFAULT = os.environ.get("MODEL_PATH", "/app/data/model.joblib")

def _plot_to_base64(fig):
    buf = io.BytesIO()
    fig.tight_layout()
    fig.savefig(buf, format="png")
    buf.seek(0)
    data = base64.b64encode(buf.read()).decode("utf-8")
    plt.close(fig)
    return data

def ensure_timestamp(df: pd.DataFrame) -> pd.DataFrame:
    if 'synthetic_timestamp' in df.columns:
        df['synthetic_timestamp'] = pd.to_datetime(df['synthetic_timestamp'])
        return df
    # create simple synthetic timestamps
    start = pd.to_datetime("2021-01-01 00:00:00")
    df = df.copy()
    df['synthetic_timestamp'] = [start + pd.Timedelta(seconds=i) for i in range(len(df))]
    return df

def select_numeric_features(df: pd.DataFrame, drop_cols=None):
    drop_cols = drop_cols or []
    numeric = df.select_dtypes(include=[np.number]).columns.tolist()
    numeric = [c for c in numeric if c not in drop_cols]
    return numeric

def train(df: pd.DataFrame, train_start: str, train_end: str, test_start: str, test_end: str, model_path: str = MODEL_PATH_DEFAULT):
    """
    df: full dataframe (must contain 'Response' and 'synthetic_timestamp')
    date strings: ISO format accepted by pandas.to_datetime
    returns: dict with metrics and base64 plots
    """
    df = ensure_timestamp(df)
    df['synthetic_timestamp'] = pd.to_datetime(df['synthetic_timestamp'])
    train_mask = (df['synthetic_timestamp'] >= pd.to_datetime(train_start)) & (df['synthetic_timestamp'] <= pd.to_datetime(train_end))
    test_mask  = (df['synthetic_timestamp'] >= pd.to_datetime(test_start))  & (df['synthetic_timestamp'] <= pd.to_datetime(test_end))

    train_df = df[train_mask].copy()
    test_df  = df[test_mask].copy()

    if train_df.empty or test_df.empty:
        raise ValueError("Train or test slice is empty")

    # target column must be 'Response' (0/1)
    if 'Response' not in train_df.columns:
        raise ValueError("Response column missing")

    # prepare features: numeric only, drop Response
    feature_cols = select_numeric_features(train_df, drop_cols=['Response'])
    if not feature_cols:
        raise ValueError("No numeric features found for training")

    X_train = train_df[feature_cols].fillna(0)
    y_train = train_df['Response'].astype(int)
    X_test  = test_df[feature_cols].fillna(0)
    y_test  = test_df['Response'].astype(int)

    # choose model
    if HAS_LGB:
        train_set = lgb.Dataset(X_train, label=y_train)
        params = {"objective":"binary", "verbosity": -1}
        model = lgb.train(params, train_set, num_boost_round=100)
        # predict_proba for consistency
        preds_proba = model.predict(X_test)
        preds = (preds_proba >= 0.5).astype(int)
        joblib.dump(model, model_path)
    else:
        model = RandomForestClassifier(n_estimators=200, random_state=42)
        model.fit(X_train, y_train)
        preds = model.predict(X_test)
        preds_proba = model.predict_proba(X_test)[:, 1]
        joblib.dump(model, model_path)

    acc = float(accuracy_score(y_test, preds))
    prec = float(precision_score(y_test, preds, zero_division=0))
    rec = float(recall_score(y_test, preds, zero_division=0))
    f1 = float(f1_score(y_test, preds, zero_division=0))
    cm = confusion_matrix(y_test, preds)
    # confusion matrix to tn, fp, fn, tp if 2x2
    if cm.size == 4:
        tn, fp, fn, tp = int(cm.ravel()[0]), int(cm.ravel()[1]), int(cm.ravel()[2]), int(cm.ravel()[3])
    else:
        tn = fp = fn = tp = 0

    # small plots
    fig1 = plt.figure()
    plt.plot([0,1,2], [0.6, 0.75, acc])  # sample epoch-like progression
    plt.title("Accuracy (sample curve)")
    plt.xlabel("epoch")
    plt.ylabel("accuracy")
    acc_plot = _plot_to_base64(fig1)

    fig2 = plt.figure()
    labels = ['TP','FP','FN','TN']
    vals = [tp, fp, fn, tn]
    plt.pie(vals, labels=labels, autopct='%1.1f%%' if sum(vals) > 0 else None)
    plt.title("Confusion breakdown")
    conf_plot = _plot_to_base64(fig2)

    result = {
        "accuracy": acc,
        "precision": prec,
        "recall": rec,
        "f1": f1,
        "tp": tp, "tn": tn, "fp": fp, "fn": fn,
        "acc_plot": acc_plot,
        "conf_plot": conf_plot,
        "feature_columns": feature_cols,
        "model_path": model_path
    }
    return result
