# Güvenlik Kararları

- Ödünç, iade, uzatma, ayırtma ve arama için admin şifresi gerekmez.
- Kayıt düzenleme/silme, rapor, kurallar, güvenlik ve yedekleme admin oturumu ister.
- Yönetici şifresi minimum 10 karakterdir.
- Beş hatalı denemeden sonra 30 saniyelik geçici kilit uygulanır.
- Görevli PIN'leri de ayrı tuzlu PBKDF2 özeti olarak saklanır; düz PIN saklanmaz.
- PIN modunda giriş yalnız aktif ve PIN tanımlı görevliyle yapılır.
- Üç güvenlik sorusunun cevapları ayrı tuzlarla özetlenir.
- Yönetici oturumu 10 dakika hareketsizlikte kapanır.
- Geri yüklemeden önce güvenlik yedeği alınır.
- Yanlış parola, bütünlük hatası ve desteklenmeyen yedek sürümü reddedilir.
