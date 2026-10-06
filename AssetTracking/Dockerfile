# ── بناء الحل كاملاً (يعمل على أي منصة Docker مثل Render) ──
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# نسخ ملفات المشاريع أولاً للاستفادة من طبقة الكاش في restore
COPY AssetTracking.sln ./
COPY src/ ./src/
RUN dotnet restore AssetTracking.sln

# نشر تطبيق الويب
RUN dotnet publish src/AssetTracking.Web/AssetTracking.Web.csproj \
    -c Release -o /app/publish --no-restore

# ── صورة التشغيل النهائية ──
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Render يتوقع الاستماع على منفذ واحد — 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080
ENTRYPOINT ["dotnet", "AssetTracking.Web.dll"]
