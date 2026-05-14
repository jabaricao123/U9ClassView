# ClassView FastAPI Backend

FastAPI version of the ClassView backend.

## Run

```powershell
cd backend-fastapi
python -m pip install -r requirements.txt
python -m uvicorn main:app --host 127.0.0.1 --port 8000 --reload
```

Open:

```text
http://127.0.0.1:8000/
```

The service reads ERP SQL Server settings from the repository root `.env` file and stores local metadata in `App_Data/classview-meta.sqlite`.
