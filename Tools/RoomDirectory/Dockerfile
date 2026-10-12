FROM python:3.12-slim
WORKDIR /app
COPY server.py .
USER nobody
EXPOSE 8080
CMD ["python", "server.py", "--bind", "0.0.0.0", "--trust-proxy"]
