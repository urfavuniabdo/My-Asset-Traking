# ── بناء تطبيق الويب (يعمل على أي منصة Docker مثل Render) ──
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# نسخ ملفات المشاريع أولاً للاستفادة من طبقة الكاش في restore
COPY AssetTracking.sln ./
COPY src/ ./src/
# مشروع الاختبارات داخل AssetTracking.sln لكنه غير منسوخ هنا — صورة النشر لا
# تحتاجه ولا تبعياته. استرجاع الحل كاملاً يفشل بـ MSB3202 على هذا السياق،
# فالاختبارات موضعها CI (ينسخ الريبو كاملاً) والنشر يسترجع مشروع الويب وحده.
RUN dotnet restore src/AssetTracking.Web/AssetTracking.Web.csproj

# نشر تطبيق الويب
RUN dotnet publish src/AssetTracking.Web/AssetTracking.Web.csproj \
    -c Release -o /app/publish --no-restore

# ── صورة التشغيل النهائية ──
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Render يتوقع الاستماع على منفذ واحد — 8080
# DOTNET_EnableWriteXorExecute=0: إلغاء W^X في الـ JIT — بدونه ينهار .NET
# بـ SIGSEGV (exit 139) داخل بيئة العزل (gVisor) التي تشغّلها Render
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_EnableWriteXorExecute=0

EXPOSE 8080
ENTRYPOINT ["dotnet", "AssetTracking.Web.dll"]
