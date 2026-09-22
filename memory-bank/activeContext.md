# Aktif Bağlam

## Güncel durum

- Aşama 0-4 tamamlandı ve doğrulandı.
- Aşama 5'in kurulum EXE'si ile 50.000/250.000 kayıt performans doğrulaması tamamlandı.
- Aşama 5'te yalnız temiz Windows 10 ve temiz Windows 11 makine doğrulaması kaldı.
- Aşama 6-9 tamamlandı ve doğrulandı.
- Kullanıcının bildirdiği eksikler Aşama 6-11 olarak planlandı; aşamalar sırayla ve ayrı doğrulama paketleri halinde uygulanacak.

## Son tamamlanan paket

- Aşama 6'da uygulama başlangıcına görevli oturumu eklendi: ortak görevli doğrudan açılır, isim seçiminde aktif görevli seçilir, PIN modunda PBKDF2 özeti doğrulanır.
- İlk kurulum sihirbazı isim/PIN modları için ilk görevli adı ve gerektiğinde PIN alır; böylece ilk açılışta kilitlenme oluşmaz.
- Ödünç, iade, uzatma ve ayırtma işlemleri aktif görevli kimliği ve adıyla işlem günlüğüne bağlanır; ödünç kaydında görevli kimliği ayrıca saklanır.
- Aşama 7'de kitap yönetimine bağımsız arama, arşivliler dahil listeleme, seçim, düzenleme, kitap düzeyinde arşivleme ve uygun kalıcı silme akışı eklendi.
- Kitap başlığı ile ilk fiziksel kopya tek veritabanı işlemi içinde kaydedilir; barkod zorunlu ve benzersizdir, hata halinde kısmi başlık bırakılmaz.
- Aşama 8'de üye yönetimine arşivliler dahil bağımsız arama/listeleme/seçim ile iletişim ve özel alan ayrıntılarını güvenli yükleyen tam CRUD eklendi.
- Ödünç kuralları, görevliler ve özel üye alanları düzenlenebilir, etkinleştirilip devre dışı bırakılabilir ve yalnız güvenliyse kalıcı silinebilir.
- Aşama 9'da Setup sürümü 1.1.0'a çıkarıldı; sabit `AppId` ve önceki kurulum diziniyle yerinde yükseltme davranışı açıkça tanımlandı.
- Gerçek `v1.0.0` Setup üzerine `v1.1.0` kuruldu; uygulama dosyası değişirken veritabanı, ayar anahtarı ve yedek dosyalarının SHA-256 özetleri korundu.
- Release derleme 0 uyarı/0 hata; güncel doğrulama çalıştırıcısı 17/17 başarılıdır.
- Aşama 10 tamamlandı: dört kurum profili ortak kurallarla çalışır; profil geçişi yeni hazır alanları ekler, eskilerini veri kaybetmeden gizler ve Sınıf/Birim tekrarını tek alana taşır.
- Aşama 11 tamamlandı: açık/koyu/Windows sistem teması, kalıcı tema tercihi, seçili gezinme, ortak odak/hover/devre dışı stilleri ve metinli vektör ikonlar eklendi.
- Şema sürümü 2'ye çıktı; eski veritabanları ve v1 yedekleri geri yükleme sırasında güvenli biçimde yükseltilir.
- `v1.2.0` Setup üretildi; Release derleme 0 uyarı/0 hata, doğrulama çalıştırıcısı 17/17 başarılıdır.
- Smart App Control imzasız Inno Setup paketini engellediği için ücretsiz Microsoft Store/MSIX dağıtım yolu hazırlandı.
- MSIX manifesti, mağaza görselleri ve `scripts/build-msix.ps1` eklendi; yer tutucu kimlikle `v1.2.0.0` x64 paketi MakeAppx doğrulamasından geçti.
- Nihai paket için Partner Center'da ürün adı ayrılması ve Ürün kimliği sayfasındaki gerçek `Identity Name` ile `Publisher` değerlerinin kullanılması bekleniyor. Yerel doğrulama paketi imzasızdır; engelin kalktığı henüz iddia edilmez.
- `v1.2.0` gerçek veritabanı ile açılırken `ThemeService.Apply` içinde salt okunur WPF fırçasını değiştirmeye çalışıp çöktü. `v1.2.1` renk kaynaklarını yenileriyle değiştiriyor ve XAML renk başvuruları dinamik yapıldı.
- `v1.2.1` Setup gerçek `v1.2.0` kurulumunun üzerine yüklendi; kurulu uygulama mevcut veritabanıyla görevli girişine ulaştı. Kurulum öncesi/sonrası veritabanı, iki koruma anahtarı ve otomatik yedek SHA-256 özetleri aynı kaldı.
- Mevcut kullanıcı veritabanının bütünlük kontrolü başarılı; şema 2, kurulum tamamlanmış, ancak 0 kitap ve 0 üye içeriyor. Kullanıcının söz ettiği eski kayıtların geldiği başka dosya/yedek henüz bulunmadı.
- Görevli PIN giriş penceresinde tema sonrası büyüyen kontroller 360 piksel sabit yüksekliğe sığmadığı için başlık ve `Giriş yap` düğmesi kırpılıyordu. Pencere içeriğe göre boyutlanır hale getirildi, tema kaynaklarına bağlandı ve PIN alanına açılış odağı eklendi.
- `v1.2.2` giriş akışı düzeltmesi: WPF'nin ilk modal pencere kapanınca uygulamayı kapatması önlendi. Başlangıç boyunca açık kapatma modu kullanılır; ana pencere açıldıktan sonra kapatma ana pencereye bağlanır. Ortak görevli modunda PIN/şifre yoktur.
- `v1.2.3` koyu tema okunabilirlik düzeltmesi: metinler, giriş denetimleri, açılır listeler, tarihler ve tablo satırları ortak dinamik paleti doğrudan kullanır; açık tema rengi koyu zeminde kalmaz.
- README'ye imzasız Setup paketinin Windows 11 Akıllı Uygulama Denetimi tarafından engellenebileceği, güvenliği kapatmanın önerilmediği ve kalıcı çözümün kod imzası/Microsoft Store olduğu eklendi.

- Proje 21 Eylül 2026'da `Qylent-Studio/Qylent-Kutuphane` adresinde herkese açık GitHub deposu olarak yayımlandı.
- `v1.0.0` GitHub Release oluşturuldu; `Qylent-Kutuphane-Setup-1.0.0.exe` kurulum dosyası Release varlığı olarak yüklendi ve GitHub SHA-256 özeti yerel dosyayla eşleşti.
- Türkçe README; kurulum, özellikler, güvenlik, sistem gereksinimleri, doğrulama durumu ve sık sorulan sorularla hazırlandı. Depo açıklaması ve arama konu etiketleri eklendi.
- Inno Setup 6.7.3 ile `Qylent-Kutuphane-Setup-1.0.0.exe` 21 Eylül 2026'da yeniden üretildi.
- Kurulum paketi mevcut Windows 11 25H2 (26200.9457) geliştirme makinesinde kullanıcı kapsamına sessizce kuruldu.
- Yalıtılmış `%LocalAppData%` ile ilk kurulum penceresinin açık, yanıt verir ve `Qylent Kütüphane — İlk kurulum` başlıklı olduğu doğrulandı; ardından kurulum temizce kaldırıldı.
- Tekrarlanabilir 50.000/250.000 kayıt performans senaryosu ve çalıştırma betiği eklendi.
- Release self-contained `win-x64` yayın yeniden üretildi.

## Doğrulama

- GitHub deposu: `https://github.com/Qylent-Studio/Qylent-Kutuphane` (`PUBLIC`, varsayılan dal `main`).
- Release: `https://github.com/Qylent-Studio/Qylent-Kutuphane/releases/tag/v1.0.0` (yayımlanmış, taslak değil, ön sürüm değil).
- Release varlığı: 52.665.821 bayt; GitHub özeti `sha256:dacfa38946b603c12bf5788c53d977c72c13483aa7c7d4d9a1ca21d200b199ea`.
- Release `dotnet build`: 0 uyarı, 0 hata.
- Konsol doğrulama paketi: 17/17 başarılı.
- 50.000 kayıt: barkod 114 ms, kitap adı 40 ms, liste 16 ms.
- 250.000 kayıt: barkod 537 ms, kitap adı 533 ms, liste 5 ms.
- Mevcut Windows 11 geliştirme makinesinde kurulum, ilk açılış ve kaldırma smoke testi başarılı.
- Yayımlanmış `v1.0.0` kurulum SHA-256: `DACFA38946B603C12BF5788C53D977C72C13483AA7C7D4D9A1CA21D200B199EA`.
- Yerel `v1.1.0` yükseltme adayı SHA-256: `EB82EC8076EE90022E7EEA0E9DF48E9EF3F2DD57300B5B9E5CB7516ED7421218`.
- Yerel `v1.2.0` Setup: 52.716.132 bayt; SHA-256 `10D68DD84AF3DF752732681375E8CFBC0246CD98629F549103F84C5C216495FA`.
- Yerel `v1.2.1` Setup: 52.696.049 bayt; SHA-256 `9B9E61DB8AEB211864530387561925513455D02782C1E9C5E5C9FB208FF79AD4`.
- `v1.0.0` → `v1.2.0` yerinde yükseltme doğrulaması başarılı; veritabanı, ayar anahtarı ve yedek dosyası korundu.
- Açık/koyu paletlerde ana metin kontrastları 5,91:1 ile 16,68:1 arasında; WCAG AA eşiğini geçiyor.
- Son `v1.2.0` Setup mevcut Windows 11 makinesinde sessizce kuruldu; yönlendirilmiş `%LocalAppData%` ile ilk kurulum penceresi açık ve yanıt verir bulundu, ardından kurulum temizce kaldırıldı.
- TestSprite CLI kimliği doğrulandı ancak yerel WPF uygulamaları için uygun web hedefi/projesi bulunmadığından TestSprite koşusu yapılamadı.
- PIN giriş penceresi düzeltmesinden sonra Release derleme 0 uyarı/0 hata ve doğrulama çalıştırıcısı 17/17 başarılıdır; PIN modu servis senaryosu ayrıca bu paket içinde geçmiştir. Derlenen uygulama gerçek kullanıcı profiliyle görevli girişine ulaştı ve pencere 460x440 olarak ölçüldü.
- `v1.2.2` self-contained yayın ve Setup üretildi: 52.690.239 bayt, SHA-256 `A021436CAA0B46D8112C1CAF046973B4C0FDA089D9E4D1EF204B9808335084AC`. Release derleme 0 uyarı/0 hata; doğrulama 17/17 başarılı. Gerçek PIN ile girişten ana pencereye geçiş ve yeni Setup kurulumu henüz uçtan uca sınanmadı.
- Nihai `v1.2.3` Setup: 52.702.059 bayt; SHA-256 `32714203C589B1D79B1894719075285537E01EC1042B9008C402B99AFF287457`. Release derleme 0 uyarı/0 hata, doğrulama 17/17 başarılı. İlk `v1.2.3` adayı `v1.0.0` üzerine veri koruyan yükseltme testinden geçmişti; son stil düzeltmesi sonrasındaki nihai Setup bu makinede Code Integrity 3077 ile engellendiği için nihai yükseltme testi tamamlanamadı.
- TestSprite 0.12.0 oturumu açık fakat proje listesi boş ve WPF masaüstü hedefi desteklenmiyor. Orca görsel otomasyon servisi bu oturumda başlamadı; açık Qylent oturumu tek örnek kilidi tuttuğu için yeni kurulu 1.2.3 ana ekranı ayrıca gözlemlenemedi.
- Yer tutucu kimlikli MSIX, tam Release ve 17/17 doğrulama sonrasında Windows SDK MakeAppx 10.0.26100.8249 ile başarıyla oluşturuldu; SHA-256 `B181505510AB4549852F2F305E098C9331CBA90BAC46385535A0F0D16F1227FF`.
- Bu makinede Hyper-V/Windows Sandbox, VirtualBox/VMware/QEMU veya hazır temiz Windows imajı bulunmadığı için temiz Windows 10/11 kabul testi yapılamadı.

## Sonraki adım

### Aşama 5'te kalan ortam doğrulaması

Temiz Windows 10 ve Windows 11 sanal/fiziksel makinelerinde `docs/calistirma-test-derleme/README.md` içindeki kabul sırasıyla kurulum, ilk açılış ve temel dolaşım smoke testi yapılmalıdır.

Microsoft Store dağıtımı için Partner Center'da **Qylent Kütüphane** adı ayrılmalı; gerçek paket kimliği değerleri alındıktan sonra MSIX yeniden üretilip sertifikasyona gönderilmelidir.
