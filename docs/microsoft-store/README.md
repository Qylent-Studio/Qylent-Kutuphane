# Microsoft Store'a Gönderim

1. [Microsoft Partner Center geliştirici hesabını](https://storedeveloper.microsoft.com/) ücretsiz oluşturun.
2. Partner Center'da **Qylent Kütüphane** ürün adını ayırın.
3. Ürünün **Ürün kimliği** sayfasından `Package/Identity/Name`, `Publisher` ve yayımlayan görünen adını alın.
4. Depo kökünde aşağıdaki komutu gerçek değerlerle çalıştırın:

```powershell
.\scripts\build-msix.ps1 `
  -IdentityName 'PARTNER_CENTER_PACKAGE_NAME' `
  -Publisher 'CN=PARTNER_CENTER_PUBLISHER' `
  -PublisherDisplayName 'Qylent Studio' `
  -Version '1.2.0.0'
```

5. `artifacts/store/Qylent-Kutuphane-1.2.0.0-x64.msix` dosyasını Partner Center gönderimine yükleyin; mağaza açıklaması, ekran görüntüleri, yaş derecelendirmesi ve gizlilik alanlarını tamamlayın.
6. Sertifikasyon tamamlandığında Microsoft paketi imzalar ve Store üzerinden güvenilir kurulum sağlar.

## Önemli

- Depodaki varsayılan kimlikler yer tutucudur. Partner Center değerleriyle yeniden üretmeden paket yüklemeyin.
- Partner Center parolası, doğrulama kodu, erişim anahtarı veya sertifika özel anahtarı depoya eklenmez ve paylaşılmaz.
- Uygulama verileri `%LocalAppData%\QylentStudio\Kutuphane` altında kalır; paket güncellemesi uygulama dosyalarını değiştirirken kullanıcı verilerini kurulum klasöründe tutmaz.
- Store gönderimi yapılana kadar `artifacts/store` altındaki yerel paket imzasızdır ve son kullanıcı dağıtımı için hazır sayılmaz.
