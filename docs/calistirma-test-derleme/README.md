# Çalıştırma, Test ve Derleme

## Gereksinimler

- Windows 10/11 x64
- Projeyle birlikte `.tools/dotnet` altına kurulan .NET 10 SDK veya sistemde .NET 10 SDK

## Komut satırı

PowerShell'de depo kökünden:

```powershell
$dotnet = '.\.tools\dotnet\dotnet.exe'
& $dotnet restore .\Qylent.Kutuphane.slnx
& $dotnet build .\Qylent.Kutuphane.slnx -c Debug --no-restore
& $dotnet run --project .\tests\Qylent.Kutuphane.Tests\Qylent.Kutuphane.Tests.csproj -c Debug --no-build
& $dotnet run --project .\src\Qylent.Kutuphane.App\Qylent.Kutuphane.App.csproj
```

## Yayın

```powershell
& $dotnet publish .\src\Qylent.Kutuphane.App\Qylent.Kutuphane.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\publish\win-x64
```

Tüm Release doğrulaması ve self-contained yayın için:

```powershell
.\scripts\build-release.ps1
```

Kurulum EXE'si için lisanslı/uygun bir Inno Setup kurulumu sonrasında:

```powershell
.\scripts\build-release.ps1 -Installer
```

Kurulum tanımı `installer/QylentKutuphane.iss` dosyasındadır. Başlat menüsü, isteğe bağlı masaüstü kısayolu ve kaldırma kaydı üretir.

## Microsoft Store MSIX paketi

Windows Uygulama Denetimi imzasız Setup dosyasını engelliyorsa ücretsiz dağıtım yolu Microsoft Store'dur. Önce Partner Center'da uygulama adını ayırın ve **Ürün kimliği** sayfasındaki `Package/Identity/Name` ile `Publisher` değerlerini alın. Parola veya erişim anahtarı paylaşmayın; bu iki kimlik değeri gizli değildir.

```powershell
.\scripts\build-msix.ps1 `
  -IdentityName 'PARTNER_CENTER_PACKAGE_NAME' `
  -Publisher 'CN=PARTNER_CENTER_PUBLISHER' `
  -PublisherDisplayName 'Qylent Studio' `
  -Version '1.2.3.0'
```

Çıktı `artifacts/store` klasörüne yazılır. Varsayılan örnek kimlikle üretilen paket yalnız yapı doğrulaması içindir; Store'a yüklenmemelidir. Son paket, Partner Center'daki değerler birebir kullanılarak yeniden üretilmeli ve Store sertifikasyonuna yüklenmelidir. Microsoft sertifikasyondan geçen paketi imzalar; imzasız yerel MSIX dosyasını doğrudan çalıştırmak Uygulama Denetimi sorununu çözmez.

Ayrıntılı gönderim sırası için `docs/microsoft-store/README.md` belgesine bakın.

## Sürüm yükseltme doğrulaması

```powershell
.\scripts\test-upgrade.ps1 `
  -OldInstaller .\artifacts\upgrade-fixtures\Qylent-Kutuphane-Setup-1.0.0.exe `
  -NewInstaller .\artifacts\installer\Qylent-Kutuphane-Setup-1.2.3.exe
```

Betik eski sürümü yalıtılmış bir dizine kurar, örnek veritabanı/ayar anahtarı/yedek oluşturur, yeni Setup'ı aynı `AppId` üzerinden çalıştırır ve uygulama güncellenirken kullanıcı verilerinin içerik özetlerinin değişmediğini doğrular. Sonunda test kurulumunu kaldırır.

## Performans doğrulaması

50.000 ve 250.000 katalog kaydı doğrulaması için:

```powershell
.\scripts\test-performance.ps1
```

Senaryo, her ölçekte yarısı kitap başlığı ve yarısı fiziksel kopya olacak şekilde veri üretir. Son kayıtta barkod ve kitap adı aramasını, ayrıca ilk katalog listesini üretim servisleri üzerinden doğrular. Her sorgu için üst sınır 3 saniyedir; ölçülen süreler konsola yazılır. Geçici veritabanları test sonunda silinir.

## Manuel test sırası

1. İlk kurulum sihirbazını tamamla ve uygulamayı yeniden aç.
2. Bir üye, kitap başlığı ve iki fiziksel kopya ekle.
3. Barkodla ödünç ver, uzat, iade et ve ayırtma sırasını doğrula.
4. Admin oturumunu kapatıp yönetim ekranlarının kilitlendiğini kontrol et.
5. Günlük ve özel aralık raporu üret; Excel'de tarih, filtre ve başlıkları kontrol et.
6. Parolalı yedek al; yanlış ve doğru parola ile geri yükleme davranışını doğrula.
7. Yönetim > Raporlar'da sütun ve ek sayfa seçimini şablon olarak kaydet, uygula ve sil.
8. Yönetim > Ayarlar'da kurum, görevli modu, varsayılan ödünç sınırları ve yedek klasörünü değiştirip uygulamayı yeniden açarak kalıcılığı kontrol et.
9. Ortak görevli, isim seçimi ve PIN modlarında uygulamayı yeniden aç; alt çubukta doğru görevlinin göründüğünü ve ödünç/iade/uzatma/ayırtma kayıtlarının işlem günlüğünde bu görevliye ait olduğunu doğrula.
10. Yönetim > Kitap yönetimi içinde arama ve seçim yap; güncelleme, arşivleme ve ilişkisiz kayıt silmeyi doğrula. Eksik veya yinelenen barkodla ekleme denemesinde kitap başlığının da oluşmadığını kontrol et.
11. Yönetim > Üye yönetimi içinde arşivliler dahil arama, seçim, ayrıntı yükleme, güncelleme, arşivleme ve uygun kalıcı silme akışlarını doğrula.
12. Kurallar ile Görevliler ve alanlar sekmelerinde birer kayıt ekle; düzenle, devre dışı bırak/etkinleştir ve güvenli silme kısıtlarını doğrula.
13. Dört kurum türünü sırayla seç; hazır alanların uyarlanıp mevcut üye verilerinin korunduğunu doğrula.
14. Görünüm sekmesinden açık, koyu ve Windows sistem temasını seç; uygulamayı yeniden açarak tercihin kalıcılığını kontrol et.
15. Yalnız klavyeyle ana gezinme ve yönetim sekmelerini dolaş; odak halkasını 100%, 125% ve 150% Windows ölçeklerinde kontrol et.

## Temiz Windows 10/11 kabul testi

Bu test geliştirme bilgisayarında değil, daha önce Qylent Kütüphane veya .NET SDK kurulmamış ayrı bir Windows 10 x64 ve ayrı bir Windows 11 x64 sanal/fiziksel makinede birer kez uygulanır.

1. En güncel `artifacts/installer/Qylent-Kutuphane-Setup-<sürüm>.exe` dosyasını makineye kopyala ve Release sayfasında yayımlanan SHA-256 değeriyle eşleştiğini doğrula.
2. Kurulumu standart kullanıcı kapsamında tamamla; başlat menüsü kısayolunu ve kaldırma kaydını kontrol et.
3. Uygulamayı aç; ilk kurulum sihirbazını tamamla ve yeniden başlattığında oluşturulan kütüphanenin açıldığını doğrula.
4. Bir üye, bir kitap başlığı ve bir fiziksel kopya oluştur; barkodla ödünç verip iade et.
5. Uygulamayı Windows Ayarları'ndan kaldır; program klasörü ve kısayolların silindiğini doğrula.
6. Windows sürümü/build numarası, test tarihi ve her adımın sonucunu `memory-bank/activeContext.md` dosyasına kaydet. Her iki işletim sistemi de başarılı olmadan Aşama 5 tamamlandı sayılmaz.

## Veri ve yedek

Canlı veri `%LocalAppData%\QylentStudio\Kutuphane` altında tutulur. Elle ve otomatik yedekler, yönetim ekranında seçilen yedekleme klasörüne yazılır. Geri yükleme işleminden önce uygulama otomatik güvenlik yedeği oluşturur. Test verilerini gerçek kullanıcı verileriyle karıştırma.
