FROM mcr.microsoft.com/devcontainers/base:bookworm

ENV DOTNET_ROOT=/usr/share/dotnet
ENV PATH="${PATH}:/usr/share/dotnet"

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl \
    && curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
    && bash /tmp/dotnet-install.sh --channel 8.0 --install-dir "${DOTNET_ROOT}" \
    && rm -f /tmp/dotnet-install.sh \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

RUN dotnet --list-sdks
