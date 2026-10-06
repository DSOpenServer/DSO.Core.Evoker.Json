# DSO.Core.Evoker.Json

> **Çalışma zamanında doğan tipler, `System.Text.Json` için elle yazılmış bir sınıftan farksız.**

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4) ![NuGet](https://img.shields.io/badge/ek%20NuGet-gerekmez-brightgreen)

`DSO.Core.Evoker.Json`, [DSO.Core.Evoker](../DSO.Core.Evoker/README.md) ile üretilen tipleri `System.Text.Json` ile
serialize ve deserialize eden ince bir köprüdür. Tek bir `JsonConverterFactory` ve üç kolaylık metodundan oluşur.
Ek bağımlılığı yoktur, çünkü `System.Text.Json` zaten .NET'in içindedir.

---

## Neden Json?

Çalışma zamanında üretilmiş bir tipi bir JSON serializer'a vermenin yaygın yolları zahmetlidir:

| Yaygın yol | Bedeli | Evoker.Json |
|---|---|---|
| Her şekil için elle converter yazmak | Her yeni şema yeni kod demektir | **Sıfır kod:** factory, çekirdeğin bildiği şemayı okur |
| Sözlük / expando ara katmanı | Gerçek tip, interface ve tip güvenliği kaybolur | Gerçek tip korunur; `Implement<T>` ile üretilenler de çalışır |
| Yansıma ile her property'yi gezmek | Her seferinde reflection ve boxing | Çekirdeğin **derlenmiş** accessor'larıyla okuma ve yazma |

Diğer özellikler:

- **İç içe dinamik tipler otomatik:** property'si başka bir dinamik tip olan nesne JSON'da gerçekten iç içe yazılır.
- **Dokunulmazlık:** normal, statik tipleriniz bu converter'dan hiç etkilenmez (`CanConvert` sadece dinamik tiplere `true`
  döner).
- **Standart ayarlar çalışır:** `PropertyNamingPolicy` (camelCase vb.) ve `PropertyNameCaseInsensitive`.

---

## Kurulum

```xml
<ProjectReference Include="..\DSO.Core.Evoker\DSO.Core.Evoker.csproj" />
<ProjectReference Include="..\DSO.Core.Evoker.Json\DSO.Core.Evoker.Json.csproj" />
```

```csharp
using DSO.Core.Evoker;
using DSO.Core.Evoker.Json;
```

---

## 60 saniyede Json

```csharp
var musteri = DynamicClass.CreateClass("Musteri")
    .AddProperty<int>("Id")
    .AddProperty<string>("Ad")
    .AddProperty<decimal>("Bakiye");

musteri.SetValue("Id", 42).SetValue("Ad", "Ayşe").SetValue("Bakiye", 1234.56m);

string json = musteri.ToJson();
// {"Id":42,"Ad":"Ay\u015Fe","Bakiye":1234.56}   (Türkçe karakterleri kaçışsız yazmak için aşağıdaki nota bakın)

DynamicClass geri = DynamicClassJsonExtensions.FromJson(musteri.Type, json);
Console.WriteLine(geri.GetValue<decimal>("Bakiye"));   // 1234.56
```

---

## API Rehberi

| Üye | Açıklama |
|---|---|
| `string ToJson(this DynamicClass dc, JsonSerializerOptions? options = null)` | Nesnenin **güncel** değerlerini JSON'a yazar |
| `DynamicClass FromJson(Type dynamicType, string json, JsonSerializerOptions? options = null)` | Verilen dinamik tipten **yeni** bir nesne üretir ve `DynamicClass.Wrap` ile sarmalar |
| `DynamicClass FromJsonUsingSchemaOf(this DynamicClass sablon, string json, JsonSerializerOptions? options = null)` | Var olan bir `DynamicClass`'ın tipini şablon olarak kullanır; şablonun kendisi **değişmez** |
| `DynamicClassJsonConverterFactory` | Standart `JsonConverterFactory`; kendi `JsonSerializerOptions`'ınıza eklenebilir |

### Serialize

```csharp
string json1 = dc.ToJson();                       // varsayılan ayarlar
string json2 = dc.ToJson(new JsonSerializerOptions { WriteIndented = true });
```

### Deserialize

```csharp
DynamicClass a = DynamicClassJsonExtensions.FromJson(dc.Type, json);   // tip elinizdeyse
DynamicClass b = dc.FromJsonUsingSchemaOf(json);                       // var olan bir nesnenin şeması üzerinden
```

### Türkçe karakterler

System.Text.Json varsayılan olarak ASCII dışı karakterleri kaçışlı yazar (`ş` → `\u015F`); JSON yine geçerlidir ve
aynen geri okunur. Okunur çıktı için encoder verin:

```csharp
var opt = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
string json = dc.ToJson(opt);   // {"Ad":"Ayşe", ...}
```

### Adlandırma politikası

```csharp
var opt = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true
};

var dc = DynamicClass.CreateClass("Kullanici").AddProperty<int>("UserId");
dc.SetValue("UserId", 7);

string json = dc.ToJson(opt);                                        // {"userId":7}
var geri = DynamicClassJsonExtensions.FromJson(dc.Type, json, opt);   // UserId = 7
```

### İç içe dinamik tipler

```csharp
var adres = DynamicClass.CreateClass("Adres").AddProperty<string>("Sehir");
var kisi = DynamicClass.CreateClass("Kisi")
    .AddProperty<string>("Ad")
    .AddProperty("Adres", adres.Type);        // property tipi başka bir dinamik tip

adres.SetValue("Sehir", "Ankara");
kisi.SetValue("Ad", "Ayşe").SetValue("Adres", adres.RawInstance);

string json = kisi.ToJson();
// {"Ad":"Ay\u015Fe","Adres":{"Sehir":"Ankara"}}
```

### Koleksiyonlar

`List<int>`, `int[]`, `Dictionary<string,int>` gibi koleksiyon tipli property'ler System.Text.Json'ın kendi
kurallarıyla yazılır ve okunur.

### Interface implement eden tipler

```csharp
using DSO.Core.Evoker.Extend;

var dc = DynamicClass.CreateClass("Musteri").Implement<ICustomer>();   // Id, Name + Validate()
dc.SetValue("Id", 1);
dc.SetMethod<Func<bool>>("Validate", () => true);

string json = dc.ToJson();   // {"Id":1,"Name":null}  - davranış (metot/event) JSON'a yansımaz
```

Ayrıntılar için [DSO.Core.Evoker.Extend](../DSO.Core.Evoker.Extend/README.md).

### Kendi seçeneklerinizle doğrudan System.Text.Json

```csharp
var opt = new JsonSerializerOptions();
opt.Converters.Add(new DynamicClassJsonConverterFactory());
opt.Converters.Add(new BenimConverterim());

string json = JsonSerializer.Serialize(dc.RawInstance, dc.Type, opt);
object? nesne = JsonSerializer.Deserialize(json, dc.Type, opt);
```

`ToJson(opt)` ve `FromJson(..., opt)` çağrıları, converter eklenmemişse onu sizin seçeneklerinize **bir kez** ekler.

> System.Text.Json bir seçenek nesnesini ilk kullanımdan sonra kilitler. Aynı `opt` nesnesini daha önce başka bir yerde
> kullandıysanız converter'ı en baştan kendiniz ekleyin.

---

## Davranış ayrıntıları

- Sadece şemadaki property'ler, yani `AddProperty` ile ya da `Implement`/`Extend` yoluyla eklenenler, serialize edilir.
  Metotlar, event'ler ve indexer'lar JSON'da görünmez.
- JSON'daki **bilinmeyen alanlar sessizce atlanır**. Bu, System.Text.Json'ın varsayılan davranışıyla tutarlıdır.
- JSON'da bulunmayan property'ler kendi tiplerinin varsayılan değerinde kalır.
- Nesne beklenen yerde başka bir JSON değeri gelirse anlaşılır bir `JsonException` alırsınız.

---

## Testler ve örnekler

`DSO.Core.Evoker.JsonTests` konsol projesi (`dotnet run --project DSO.Core.Evoker.JsonTests`):

| Test | Kapsam |
|---|---|
| J1 | Temel gidiş-dönüş: `int`, `string`, `decimal` |
| J2 | JSON alan adlarının property adlarıyla birebir eşleşmesi |
| J3 | camelCase politikası: yazma ve geri okuma |
| J4 | Bilinmeyen alanların hata vermeden atlanması |
| J5 | İç içe dinamik tipler: gerçek iç içe JSON |
| J6 | `Implement<T>()` ile üretilmiş tipler |
| J7 | Normal tiplerin etkilenmemesi (`CanConvert` = false) |
| J8 | `FromJsonUsingSchemaOf`: şablonun değişmemesi |

---

## İlgili paketler

- [DSO.Core.Evoker](../DSO.Core.Evoker/README.md): `DynamicClass`, `DynamicTypeFactory.GetSchema` / `IsDynamicType`.
- [DSO.Core.Evoker.Extend](../DSO.Core.Evoker.Extend/README.md): interface ve taban sınıf implementasyonu.
- Not: [DSO.Core.Evoker.Api](../DSO.Core.Evoker.Api/README.md) ve [Plugins](../DSO.Core.Evoker.Plugins/README.md) kendi
  JSON ihtiyaçları için çekirdeğin `EvokerJson` ayarlarını kullanır; bu paket onlar için gerekli değildir.
