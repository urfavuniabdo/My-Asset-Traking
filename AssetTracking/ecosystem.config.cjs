// PM2 — تشغيل مخرجات النشر مباشرة (بدون MSBuild) لتوفير الذاكرة
module.exports = {
  apps: [
    {
      name: 'ats',
      script: '/home/user/.dotnet/dotnet',
      args: '/home/user/app_published/AssetTracking.Web.dll',
      cwd: '/home/user/app_published',
      interpreter: 'none',
      env: {
        DOTNET_ROOT: '/home/user/.dotnet',
        ASPNETCORE_ENVIRONMENT: 'Development',
        ASPNETCORE_URLS: 'http://0.0.0.0:3000',
        // البيئة بها ٩٨٥ ميجا رام فقط — نحدّ من استهلاك الـGC
        DOTNET_gcServer: '0',
        DOTNET_GCHeapHardLimit: '0x10000000', // 256 ميجا
        DOTNET_TieredPGO: '0'
      },
      watch: false,
      instances: 1,
      exec_mode: 'fork',
      max_restarts: 5,
      min_uptime: 10000,
      out_file: '/home/user/ats-out.log',
      error_file: '/home/user/ats-err.log',
      merge_logs: true
    }
  ]
};
