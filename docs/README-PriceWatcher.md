# PriceWatcher

Hepsiburada ürünlerinin fiyatlarını düzenli aralıklarla kontrol ederek değişim olduğunda Telegram abonelerine bildirim gönderen arkaplan servisi.

## Gereksinimler
- .NET 9 SDK
- Telegram bot token ve admin chat id (BotFather üzerinden alınabilir)
- İnternet erişimi

## Çalıştırma Adımları
1. Telegram BotFather ile yeni bir bot oluşturun ve token alın.
2. `src/PriceWatcher.App/appsettings.json` dosyasına bot token ve admin chat id değerlerini girin.
3. İzlemek istediğiniz Hepsiburada ürün bağlantılarını `Hepsiburada:ProductUrls` listesine ekleyin.
4. Proje klasöründe aşağıdaki komutla çalıştırın:
   ```bash
   dotnet run --project src/PriceWatcher.App
   ```
5. Telegram'da botunuza `/start` yazarak abone olun ve bildirimleri almaya başlayın.

## Konfigürasyon Notları
- `RequestIntervalMinutes`: Fiyat kontrol aralığı (dakika cinsinden).
- `NotifyOnEveryPull`: Her çekimde fiyat bilgisini gönderip göndermeyeceğinizi belirler.
- `MinChangePercentageToNotify`: Yüzde bazında minimum değişim eşiği (NotifyOnEveryPull false iken geçerli).
- Veritabanı `Storage:DbPath` alanında belirtilen LiteDB dosyasında tutulur.

## Güvenlik ve Dikkat
- Bot token ve kişisel chat id bilgilerini versiyon kontrolüne eklemeyin.
- Çok sık istek göndermek Hepsiburada tarafından engellenmeye sebep olabilir; makul aralıklar kullanın.
