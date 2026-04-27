# syntax=docker/dockerfile:1.7

# ============================================================
# Stage 1: build
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj first to leverage Docker layer cache for restore
COPY src/APIHU/APIHU.csproj ./APIHU/
RUN dotnet restore ./APIHU/APIHU.csproj

# Copy the rest of the source and publish
COPY src/APIHU/. ./APIHU/
RUN dotnet publish ./APIHU/APIHU.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# ============================================================
# Stage 2: runtime
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Render / Fly / Railway inyectarán PORT como variable de entorno.
# Nuestro Program.cs lee PORT y bindea http://0.0.0.0:{PORT}.
# Default 8080 si la plataforma no lo setea.
ENV PORT=8080
EXPOSE 8080

# Reduce el tamaño del workdir y consumo de memoria deshabilitando ICU
# (no lo necesitamos para nuestro caso — todo es texto en español ya tokenizado).
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true

ENTRYPOINT ["dotnet", "APIHU.dll"]
