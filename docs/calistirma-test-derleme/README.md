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

## Temiz Windows 10/11 kabul testi

Bu test geliştirme bilgisayarında değil, daha önce Qylent Kütüphane veya .NET SDK kurulmamış ayrı bir Windows 10 x64 ve ayrı bir Windows 11 x64 sanal/fiziksel makinede birer kez uygulanır.

1. `artifacts/installer/Qylent-Kutuphane-Setup-1.0.0.exe` dosyasını makineye kopyala ve SHA-256 değerinin `DACFA38946B603C12BF5788C53D977C72C13483AA7C7D4D9A1CA21D200B199EA` olduğunu doğrula.
2. Kurulumu standart kullanıcı kapsamında tamamla; başlat menüsü kısayolunu ve kaldırma kaydını kontrol et.
3. Uygulamayı aç; ilk kurulum sihirbazını tamamla ve yeniden başlattığında oluşturulan kütüphanenin açıldığını doğrula.
4. Bir üye, bir kitap başlığı ve bir fiziksel kopya oluştur; barkodla ödünç verip iade et.
5. Uygulamayı Windows Ayarları'ndan kaldır; program klasörü ve kısayolların silindiğini doğrula.
6. Windows sürümü/build numarası, test tarihi ve her adımın sonucunu `memory-bank/activeContext.md` dosyasına kaydet. Her iki işletim sistemi de başarılı olmadan Aşama 5 tamamlandı sayılmaz.

## Veri ve yedek

Canlı veri `%LocalAppData%\QylentStudio\Kutuphane` altında tutulur. Elle ve otomatik yedekler, yönetim ekranında seçilen yedekleme klasörüne yazılır. Geri yükleme işleminden önce uygulama otomatik güvenlik yedeği oluşturur. Test verilerini gerçek kullanıcı verileriyle karıştırma.
