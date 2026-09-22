# Teknik Bağlam

## Araçlar

- .NET SDK 10.0.401, proje içindeki `.tools/dotnet` altında yerel kurulum.
- WPF, XAML, MVVM.
- EF Core SQLite 10.0.12 ve güvenlik düzeltmeli SQLitePCLRaw 3.0.5.
- ClosedXML 0.105.x.
- Bağımlılıksız konsol doğrulama çalıştırıcısı. Bu makinedeki Uygulama Denetimi, test framework'lerinin alt süreçte DLL yüklemesini engellediği için testler doğrudan .NET işleminde çalışır.
- Türkçe karakterli çalışma yolunda Uygulama Denetimi engeli oluşursa `--artifacts-path C:\KutuphaneBuild` ile ASCII çıktı klasörü kullanılır.

## Hedef

- `net10.0-windows10.0.19041.0`
- `win-x64`, self-contained yayın.
- Doğrulanmış yayın: `artifacts/publish/win-x64/Qylent.Kutuphane.exe` (yaklaşık 184 MB).
- Kurulum tanımı: `installer/QylentKutuphane.iss`.
- Inno Setup 6.7.3 kurulu ve doğrulandı.
- Yayımlanmış kurulum: `v1.0.0`, SHA-256 `DACFA38946B603C12BF5788C53D977C72C13483AA7C7D4D9A1CA21D200B199EA`.
- Yerel güncel kurulum adayı: `artifacts/installer/Qylent-Kutuphane-Setup-1.2.3.exe`, SHA-256 `32714203C589B1D79B1894719075285537E01EC1042B9008C402B99AFF287457`; nihai paket bu makinede Akıllı Uygulama Denetimi tarafından engellendi, henüz GitHub Release olarak yayımlanmadı.
- GitHub deposu: `https://github.com/Qylent-Studio/Qylent-Kutuphane`; varsayılan dal `main`.
- GitHub Release: `v1.0.0`; Setup EXE doğrudan Release varlığı olarak yayımlandı.
- Performans doğrulaması: `scripts/test-performance.ps1`; 50.000 ve 250.000 katalog kaydı, sorgu başına 3 saniye üst sınırı.
- Güncel doğrulama çalıştırıcısı: 17 senaryo; dört kurum profili, veri koruyan profil geçişi ve tema kalıcılığı dahildir.
- Yerinde yükseltme doğrulaması: `scripts/test-upgrade.ps1`; gerçek `v1.0.0` ve `v1.2.0` Setup paketleriyle yalıtılmış yükseltme doğrulandı. Gerçek kullanıcı kurulumunda `v1.2.0` → `v1.2.1` sırasında veritabanı, anahtarlar ve yedek dosyası özetleri korundu.
- WPF uygulaması PerMonitorV2 DPI farkındalığı kullanır; ortak tema kaynakları açık/koyu/sistem paletlerini uygular.
- Microsoft Store paketi: `packaging/msix/AppxManifest.xml`, `packaging/msix/Assets` ve `scripts/build-msix.ps1`.
- Yerel yapı doğrulaması: Windows SDK MakeAppx 10.0.26100.8249 ile `artifacts/store/Qylent-Kutuphane-1.2.0.0-x64.msix`; nihai kimlik ve imza Partner Center gönderiminde tamamlanır.

## Veri konumu

Üretim verileri `%LocalAppData%\QylentStudio\Kutuphane` altında tutulur. Depoya veritabanı, yedek, Excel çıktısı veya gizli değer eklenmez.

## Güvenlik

Şifreler ve güvenlik cevapları tuzlanmış PBKDF2 özetleri olarak saklanır. Hassas alan anahtarı Windows DPAPI ile korunur. Yedek kapsayıcıları parola tabanlı AES-GCM ile şifrelenir.
