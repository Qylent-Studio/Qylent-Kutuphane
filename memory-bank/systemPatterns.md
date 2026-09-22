# Sistem Kalıpları

## Katmanlar

- `Qylent.Kutuphane.Core`: varlıklar, kurallar, sonuç tipleri ve servis sözleşmeleri.
- `Qylent.Kutuphane.Infrastructure`: EF Core, güvenlik, rapor ve yedekleme uygulamaları.
- `Qylent.Kutuphane.App`: WPF görünüm, ViewModel ve uygulama başlangıcı.
- `Qylent.Kutuphane.Tests`: birim ve entegrasyon testleri.

## Temel ilkeler

- Kitap başlığı bibliyografik kaydı, kitap kopyası fiziksel envanteri temsil eder.
- Geçmişi bulunan kayıtlar silinmez; arşivlenir.
- Ödünç ve ayırtma değişiklikleri tek veritabanı işlemi içinde yapılır.
- Kitap başlığı ile ilk fiziksel kopya tek veritabanı işlemi içinde oluşturulur.
- Uygulama açılışında tek bir aktif görevli oturumu kurulur; dolaşım komutları bu oturumun kimliğini servis katmanına taşır.
- Audit kayıtları uygulamadan düzenlenemez.
- İşlem geçmişi bulunan görevliler ve değeri bulunan özel üye alanları kalıcı silinmez; devre dışı bırakılır.
- Setup dosyaları uygulama dizinini günceller; canlı veriler ayrı `%LocalAppData%\QylentStudio\Kutuphane` dizininde kaldığı için aynı `AppId` ile yerinde yükseltmede korunur.
- Kurum profili hazır alanları `ProfileKey` ile sistem alanı olarak izlenir; profil geçişinde eski alanlar silinmez, devre dışı bırakılır.
- `Member.ClassOrUnit`, okul ve özel kurum için ortak tek Sınıf/Birim kaynağıdır; eski yinelenen alanlar şema geçişinde buraya taşınır.
- Tema tercihi kütüphane profilinde saklanır; `ThemeService` ortak WPF fırçalarını açık, koyu veya Windows sistem temasına göre günceller.
- Dış ağ API'si yoktur; servisler uygulama içinde kullanılır.
- UTC saklanır, Türkiye yerel biçimiyle gösterilir.
