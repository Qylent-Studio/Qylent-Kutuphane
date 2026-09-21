# İlerleme

- [x] Aşama 0: Planlama ve temeller
  - [x] .NET 10 çözümü; App, Core, Infrastructure ve doğrulama projeleri
  - [x] Yerel Git ve güvenli `.gitignore`
  - [x] Memory Bank ve çalıştırma/test/derleme belgesi
  - [x] Restore ve uyarısız derleme doğrulaması
- [x] Aşama 1: İlk kurulum, güvenlik ve veri temeli
  - [x] Atomik ilk kurulum sihirbazı ve profil türleri
  - [x] PBKDF2 şifre/cevap özetleri, giriş kilidi ve 10 dakikalık admin oturumu
  - [x] AES-GCM hassas alan şifreleme ve Windows DPAPI anahtar koruması
  - [x] Tek uygulama örneği ve UTC tarih saklama
- [x] Aşama 2: Kitap ve üye yönetimi
  - [x] Kitap başlığı ile fiziksel kopya ayrımı ve benzersiz barkod
  - [x] Kitap adı, yazar, ISBN ve barkod araması
  - [x] Kitap, kopya ve üye ekleme/düzenleme
  - [x] Geçmişli kayıtları arşivleme; yalnız ilişkisiz hatalı kayıtları kalıcı silme
  - [x] Kurulum profiline göre hazır üye alanları ve yönetici tanımlı özel alanlar
  - [x] Metin, sayı, tarih, seçim ve evet/hayır özel alan doğrulaması
- [x] Aşama 3: Ödünç, iade, uzatma ve ayırtma
  - [x] Ödünç süresi, aktif kitap ve uzatma sınırları
  - [x] Barkodla ödünç/iade, uzatma ve gecikme takibi
  - [x] FIFO ayırtma sırası ve iade edilen kopyayı sıradaki üyeye ayırma
- [x] Aşama 4: Admin, Excel raporları ve yedekleme
  - [x] Admin kilidi; kurallar, görevliler, özel alanlar ve işlem günlüğü
  - [x] `.xlsx` rapor üretimi ve şifreli elle/otomatik yedek
  - [x] Rapor şablonlarını kaydetme, uygulama ve silme
  - [x] Günlük/haftalık/aylık/özel rapor dönemi ve sütun seçimi
  - [x] Kurum, görevli, ödünç ve yedek ayarlarının UI'dan düzenlenmesi
- [~] Aşama 5: Arayüz, paketleme ve son doğrulama
  - [x] Hallmark Workbench/kobalt WPF arayüzü
  - [x] Self-contained `win-x64` yayın tanımı ve doğrulanmış yayın çıktısı
  - [x] Inno Setup kurulum betiği
  - [x] Inno Setup 6.7.3 ile kurulum EXE derlemesi
  - [x] Public GitHub deposu, kaliteli README ve `v1.0.0` Setup EXE Release yayını
  - [x] Mevcut Windows makinesinde sessiz kurulum ve kaldırma smoke testi
  - [x] 50.000/250.000 kayıt performans doğrulaması
  - [ ] Uygulama açılışında ayarlanan moda göre görevli seçimi/PIN doğrulaması
  - [ ] Ödünç, iade ve ayırtma işlemlerini giriş yapan görevliye bağlama
  - [ ] Temiz Windows 10 ve temiz Windows 11 kurulum/ilk açılış doğrulaması

## Plan kapsamı

- Tanımlı aşamalar 0-5 arasındadır; toplam 6 aşama vardır.
- Aşama 6 için henüz kapsam tanımlanmamıştır.

## Son doğrulama

- Release derleme: 0 uyarı, 0 hata.
- Doğrulama çalıştırıcısı: 11 başarılı, 0 başarısız.
- Performans: 50.000 kayıtta en yavaş sorgu 114 ms; 250.000 kayıtta 537 ms.
- Kurulum EXE üretimi ile mevcut Windows 11 25H2 geliştirme makinesinde kurulum, ilk açılış ve kaldırma smoke testi başarılı.
- GitHub `v1.0.0` Release içindeki Setup EXE boyutu ve SHA-256 özeti canlı olarak doğrulandı.
- Görevli/PIN kayıt altyapısı vardır; görevli giriş ekranı, PIN doğrulaması ve aktif görevlinin dolaşım işlemlerine aktarılması henüz yoktur.
- Temiz Windows 10/11 ortamı bu makinede bulunmadığı için ilgili kabul maddesi açık kaldı.
- Tamamlanan aşama: 5/6 (Aşama 0-4). Aşama 5 görevli giriş akışı, işlemlerin aktif görevliye bağlanması ve temiz Windows 10/11 doğrulamasını bekliyor.
