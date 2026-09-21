# Aktif Bağlam

## Güncel durum

- Aşama 0-4 tamamlandı ve doğrulandı.
- Aşama 5'in kurulum EXE'si ile 50.000/250.000 kayıt performans doğrulaması tamamlandı.
- Aşama 5'te yalnız temiz Windows 10 ve temiz Windows 11 makine doğrulaması kaldı.
- Proje planında Aşama 6 tanımlı değildir; kapsam kullanıcıyla netleştirilmeden yeni kapsam eklenmeyecek.

## Son tamamlanan paket

- Inno Setup 6.7.3 ile `Qylent-Kutuphane-Setup-1.0.0.exe` 21 Eylül 2026'da yeniden üretildi.
- Kurulum paketi mevcut Windows 11 25H2 (26200.9457) geliştirme makinesinde kullanıcı kapsamına sessizce kuruldu.
- Yalıtılmış `%LocalAppData%` ile ilk kurulum penceresinin açık, yanıt verir ve `Qylent Kütüphane — İlk kurulum` başlıklı olduğu doğrulandı; ardından kurulum temizce kaldırıldı.
- Tekrarlanabilir 50.000/250.000 kayıt performans senaryosu ve çalıştırma betiği eklendi.
- Release self-contained `win-x64` yayın yeniden üretildi.

## Doğrulama

- Release `dotnet build`: 0 uyarı, 0 hata.
- Konsol doğrulama paketi: 11/11 başarılı.
- 50.000 kayıt: barkod 114 ms, kitap adı 40 ms, liste 16 ms.
- 250.000 kayıt: barkod 537 ms, kitap adı 533 ms, liste 5 ms.
- Mevcut Windows 11 geliştirme makinesinde kurulum, ilk açılış ve kaldırma smoke testi başarılı.
- Yeni kurulum SHA-256: `DACFA38946B603C12BF5788C53D977C72C13483AA7C7D4D9A1CA21D200B199EA`.
- Bu makinede Hyper-V/Windows Sandbox, VirtualBox/VMware/QEMU veya hazır temiz Windows imajı bulunmadığı için temiz Windows 10/11 kabul testi yapılamadı.

## Sonraki adım

Temiz Windows 10 ve Windows 11 sanal/fiziksel makinelerinde `docs/calistirma-test-derleme/README.md` içindeki kabul sırasıyla kurulum, ilk açılış ve temel dolaşım smoke testi yapılmalıdır. Ardından Aşama 5 tamamlanabilir; Aşama 6 kapsamı ancak kullanıcı tanımlarsa eklenir.
