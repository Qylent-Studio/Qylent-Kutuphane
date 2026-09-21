# Teknik Bağlam

## Araçlar

- .NET SDK 10.0.401, proje içindeki `.tools/dotnet` altında yerel kurulum.
- WPF, XAML, MVVM.
- EF Core SQLite 10.0.12 ve güvenlik düzeltmeli SQLitePCLRaw 3.0.5.
- ClosedXML 0.105.x.
- Bağımlılıksız konsol doğrulama çalıştırıcısı. Bu makinedeki Uygulama Denetimi, test framework'lerinin alt süreçte DLL yüklemesini engellediği için testler doğrudan .NET işleminde çalışır.

## Hedef

- `net10.0-windows10.0.19041.0`
- `win-x64`, self-contained yayın.
- Doğrulanmış yayın: `artifacts/publish/win-x64/Qylent.Kutuphane.exe` (yaklaşık 184 MB).
- Kurulum tanımı: `installer/QylentKutuphane.iss`.
- Inno Setup 6.7.3 kurulu ve doğrulandı.
- Üretilen kurulum: `artifacts/installer/Qylent-Kutuphane-Setup-1.0.0.exe` (21 Eylül 2026 üretimi, SHA-256 `DACFA38946B603C12BF5788C53D977C72C13483AA7C7D4D9A1CA21D200B199EA`).
- Performans doğrulaması: `scripts/test-performance.ps1`; 50.000 ve 250.000 katalog kaydı, sorgu başına 3 saniye üst sınırı.

## Veri konumu

Üretim verileri `%LocalAppData%\QylentStudio\Kutuphane` altında tutulur. Depoya veritabanı, yedek, Excel çıktısı veya gizli değer eklenmez.

## Güvenlik

Şifreler ve güvenlik cevapları tuzlanmış PBKDF2 özetleri olarak saklanır. Hassas alan anahtarı Windows DPAPI ile korunur. Yedek kapsayıcıları parola tabanlı AES-GCM ile şifrelenir.
