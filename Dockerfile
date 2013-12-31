FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine AS base
LABEL Maintainer="WeihanLi"

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build-env

WORKDIR /app
COPY ./src/ ./src/
COPY ./build/ ./build/
COPY ./Directory.Build.props ./
COPY ./Directory.Build.targets ./
COPY ./Directory.Packages.props ./
WORKDIR /app/src/dotnet-exec/
ENV HUSKY=0
RUN dotnet publish -f net8.0 -o /app/artifacts

FROM base AS final
WORKDIR /app
COPY --from=build-env /app/artifacts/ ./
ENV PATH="/app:${PATH}"
ENTRYPOINT [ "dotnet-exec" ]
