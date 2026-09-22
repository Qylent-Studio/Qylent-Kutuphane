# Qylent Kütüphane

**Windows 10/11 için çevrimdışı çalışan, Türkçe kütüphane yönetim ve kitap takip programı.**

[![Sürüm](https://img.shields.io/github/v/release/Qylent-Studio/Qylent-Kutuphane?display_name=tag&sort=semver)](https://github.com/Qylent-Studio/Qylent-Kutuphane/releases/latest)
[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-2563eb?logo=windows11&logoColor=white)](https://github.com/Qylent-Studio/Qylent-Kutuphane/releases/latest)
[![.NET](https://img.shields.io/badge/.NET-10-512bd4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)

Qylent Kütüphane; okul, halk ve kurum kütüphanelerinin kitap, fiziksel kopya, üye, ödünç, iade, uzatma ve ayırtma işlemlerini tek bir Windows bilgisayarında yönetmesini sağlar. İnternet veya ayrı bir sunucu gerektirmez; veriler kurumun kendi bilgisayarında kalır.

> **Hazır kurulum:** [En güncel Windows Setup EXE dosyasını indir](https://github.com/Qylent-Studio/Qylent-Kutuphane/releases/latest)

## Öne çıkan özellikler

- Kitap başlıkları ve fiziksel kopyalar için ayrı envanter yönetimi
- Kitap adı, yazar, ISBN ve barkod ile hızlı arama
- Üye kaydı ve kuruma göre özelleştirilebilir üye alanları
- Veri kaybetmeden okul, halk, özel kurum ve genel profil geçişi
- Kalıcı açık, koyu veya Windows sistem teması
- Barkodla ödünç verme, iade, uzatma ve gecikme takibi
- FIFO sırasına göre ayırtma yönetimi
- Yönetici korumalı ayarlar, işlem günlüğü ve güvenlik alanı
- Günlük, haftalık, aylık veya özel aralıklı `.xlsx` raporlar
- Parola korumalı elle ve otomatik yedekleme
- Tamamen Türkçe, klavye dostu masaüstü arayüzü

## Kurulum

1. [Releases](https://github.com/Qylent-Studio/Qylent-Kutuphane/releases/latest) sayfasını açın.
2. En güncel `Qylent-Kutuphane-Setup-1.2.3.exe` dosyasını indirin.
3. Kurulum sihirbazını tamamlayın ve Başlat menüsünden **Qylent Kütüphane** uygulamasını açın.
4. İlk açılışta kurum profilini ve yönetici hesabını oluşturun.

Uygulama `win-x64` için self-contained yayımlanır; son kullanıcının ayrıca .NET kurmasına gerek yoktur.

### Windows Akıllı Uygulama Denetimi

Setup EXE henüz güvenilir bir sertifika yetkilisinden alınmış kod imzası taşımadığı için Windows 11 **Akıllı Uygulama Denetimi** kurulumu engelleyebilir. Microsoft'un [açıklamasına](https://learn.microsoft.com/en-us/windows/apps/develop/smart-app-control/overview) göre bilinmeyen ve imzasız uygulamalar varsayılan olarak engellenebilir. Dosyanın bütünlüğünü doğrulamak bu engeli otomatik olarak kaldırmaz.

Nihai `v1.2.3` Setup dosyasının geliştirme makinesindeki kurulumu bu ilke tarafından engellendi. Bu nedenle bu dosyanın kurulum/yükseltme testi tamamlanmadı; [sürüm notundaki doğrulama sınırlarını](docs/release-notes/v1.2.3.md) göz önünde bulundurun.

- Akıllı Uygulama Denetimi'ni kapatmanızı önermiyoruz.
- Kurulum dosyasını yalnızca bu deponun resmi [Releases](https://github.com/Qylent-Studio/Qylent-Kutuphane/releases/latest) sayfasından indirin.
- Dosyayı aşağıdaki SHA-256 yöntemiyle doğrulayın.
- Kalıcı çözüm kod imzalı paket veya Microsoft Store dağıtımıdır; Store paketi hazırlığı sürmektedir.

### Dosya bütünlüğü

`v1.2.3` kurulum dosyasının SHA-256 özeti: `32714203C589B1D79B1894719075285537E01EC1042B9008C402B99AFF287457`. PowerShell ile kontrol:

```powershell
Get-FileHash .\Qylent-Kutuphane-Setup-1.2.3.exe -Algorithm SHA256
```

## Gizlilik ve güvenlik

Qylent Kütüphane bulut hizmeti kullanmaz. Canlı veriler `%LocalAppData%\QylentStudio\Kutuphane` altında yerel olarak tutulur.

- Yönetici parolaları ve güvenlik cevapları düz metin saklanmaz.
- Hassas veriler Windows DPAPI ile korunan bir anahtarla şifrelenir.
- Yedekler parola tabanlı AES-GCM şifrelemesi kullanır.
- Yönetici oturumu hareketsizlikte otomatik olarak kapanır.
- Ödünç ve ayırtma gibi ilişkili işlemler veritabanı işlemleriyle korunur.

## Kimler için uygun?

- Okul kütüphaneleri
- Küçük ve orta ölçekli halk kütüphaneleri
- Dernek, vakıf ve kurum kütüphaneleri
- İnternetsiz veya yerel veri saklama gereksinimi olan arşivler

## Sistem gereksinimleri

| Bileşen | Gereksinim |
| --- | --- |
| İşletim sistemi | Windows 10 veya Windows 11, x64 |
| İnternet | Gerekmez |
| Sunucu | Gerekmez |
| Ek çalışma zamanı | Gerekmez; kurulum self-contained'dır |
| Veri konumu | Yerel kullanıcı profili |

## Geliştirme

Proje .NET 10, WPF, Entity Framework Core ve SQLite kullanır. Katmanlar:

- `Qylent.Kutuphane.App`: WPF arayüzü ve uygulama başlangıcı
- `Qylent.Kutuphane.Core`: alan modeli, kurallar ve servis sözleşmeleri
- `Qylent.Kutuphane.Infrastructure`: SQLite, güvenlik, rapor ve yedekleme
- `Qylent.Kutuphane.Tests`: bağımlılıksız doğrulama çalıştırıcısı

Derleme, test, performans ve kurulum paketi üretme adımları için [çalıştırma, test ve derleme kılavuzuna](docs/calistirma-test-derleme/README.md) bakın.

## Doğrulama durumu

- Release derleme: **0 uyarı, 0 hata**
- Otomatik doğrulama: **17/17 başarılı**
- 50.000 ve 250.000 kayıtlı performans senaryoları: **başarılı**
- Mevcut Windows 11 geliştirme makinesinde kurulum, ilk açılış ve kaldırma testi: **başarılı**
- Ayrı, temiz Windows 10 ve Windows 11 makinelerinde kabul testi: **henüz tamamlanmadı**

## Sık sorulan sorular

### Qylent Kütüphane internet olmadan çalışır mı?

Evet. Uygulama, veritabanı ve yedekleme işlevleri yerel çalışır; bulut hesabı veya ayrı sunucu gerekmez.

### Birden fazla bilgisayar arasında eşitleme yapar mı?

Hayır. Uygulama tek Windows bilgisayarında çevrimdışı kullanım için tasarlanmıştır.

### Barkod okuyucu destekleniyor mu?

Klavye gibi giriş yapan standart barkod okuyucularla kitap kopyası arama, ödünç verme ve iade akışları kullanılabilir.

### Veriler nerede saklanır?

Uygulama verileri Windows kullanıcı profilindeki `%LocalAppData%\QylentStudio\Kutuphane` klasöründe tutulur.

### Excel raporu alınabilir mi?

Evet. Tarih aralığı, sayfalar ve sütunlar seçilerek `.xlsx` raporları üretilebilir; seçimler rapor şablonu olarak kaydedilebilir.

## Kapsam

İlk sürümde bulut eşitleme, mobil istemci, SMS/e-posta bildirimi, para cezası hesaplama ve Excel'den veri içe aktarma bulunmaz.

## Yayıncı

**Qylent Studio**

Hata bildirmek veya öneride bulunmak için [GitHub Issues](https://github.com/Qylent-Studio/Qylent-Kutuphane/issues) bölümünü kullanabilirsiniz.

> Bu depoda henüz bir açık kaynak lisansı tanımlanmamıştır. Kaynak kodun yayımlanmış olması, otomatik olarak kullanım, değiştirme veya yeniden dağıtma izni vermez.
