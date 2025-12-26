FROM mcr.microsoft.com/dotnet/sdk:8.0

WORKDIR /app

# 必要なツールのインストール
RUN apt-get update && apt-get install -y \
    libgdiplus \
    libc6-dev \
    && rm -rf /var/lib/apt/lists/*

# ビルド用のボリュームをマウント
VOLUME /app
