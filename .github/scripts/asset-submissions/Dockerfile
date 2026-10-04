# Official Python image; no source or dependencies are supplied by the PR.
FROM python:3.12.14-slim-bookworm
RUN python -m pip install --no-cache-dir --only-binary=:all: \
    onnx==1.23.1 numpy==2.5.3 protobuf==7.36.2 \
    typing_extensions==4.16.0 ml_dtypes==0.6.0
COPY validator.py test_validator.py /validator/
WORKDIR /validator
ENV OPENBLAS_NUM_THREADS=1 OMP_NUM_THREADS=1 PYTHONDONTWRITEBYTECODE=1
USER 65534:65534
ENTRYPOINT ["python", "-I", "/validator/validator.py"]
