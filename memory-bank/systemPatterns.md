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
- Audit kayıtları uygulamadan düzenlenemez.
- Dış ağ API'si yoktur; servisler uygulama içinde kullanılır.
- UTC saklanır, Türkiye yerel biçimiyle gösterilir.

