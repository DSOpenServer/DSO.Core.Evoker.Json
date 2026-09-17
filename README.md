# DSO.Core.Evoker.Json

**Runtime'da ürettiğiniz tipler, `System.Text.Json`'a sizin tanımladığınız kadar sıradan görünsün.**

`DSO.Core.Evoker.Json`, [`DSO.Core.Evoker`](../DSO.Core.Evoker/README.md) ile üretilen tipleri `System.Text.Json` ile serialize/deserialize etmenizi sağlayan, **tek satır ekstra kod gerektirmeyen** ince bir köprüdür. Hiçbir NuGet bağımlılığı eklemez — `System.Text.Json` zaten .NET'in kendi BCL'sinde geldiği için bu paket sadece core'a referans verir.

---

## Neden var

Runtime'da üretilen bir tipi standart bir JSON serializer'a vermek genelde iki şekilde çözülür:

- **Serializer'ın "bilmediği" bir tip olduğu için elle yazılmış bir dönüştürücü** — her yeni şekil için yeni kod yazmanız gerekir.
- **Dictionary/`ExpandoObject` tabanlı bir ara adım** — çalışır ama sizi "gerçek tip" avantajından (derleme zamanı property adları, tip güvenliği, interface implementasyonu) mahrum bırakır.

`DSO.Core.Evoker.Json`, `System.Text.Json`'ın **`JsonConverterFactory`** genişletme noktasını kullanarak üçüncü bir yol sunuyor: `DynamicTypeFactory`'nin ürettiği **herhangi bir tipi** otomatik tanıyıp, elle dönüştürücü yazmanıza hiç gerek kalmadan serialize/deserialize ediyor — çünkü zaten core'un kendisi her tipin şemasını (`GetSchema`) biliyor, biz sadece onu okuyoruz.

---

## Öne çıkan özellikler

### 1. Sıfır ekstra bağımlılık
Sadece core'a referans verir. `System.Text.Json` zaten .NET'in parçası — NuGet'e gitmenize gerek yok.

### 2. Otomatik tip tanıma
`JsonSerializer.Serialize`/`Deserialize`'a hiçbir özel ayar vermenize gerek yok ötesinde — factory, `DynamicTypeFactory.IsDynamicType(Type)` ile "bu tipi ben üretmedim mi?" diye soruyor ve öyleyse devreye giriyor. Diğer (normal, statik) tipleriniz hiç etkilenmiyor.

### 3. İç içe (nested) nesneler otomatik çalışır
Bir dinamik tipin property'si başka bir dinamik tipse, JSON çıktısında da **gerçekten iç içe** bir nesne olarak görünür — bunun için hiçbir ekstra kod yazmanıza gerek yok, `System.Text.Json`'ın kendi recursive serialize mekanizması bizim factory'mizi otomatik olarak tekrar tetikliyor.

### 4. `Implement<T>`/`Extend<T>` ile üretilen tiplerle de çalışır
[`DSO.Core.Evoker.Extend`](../DSO.Core.Evoker.Extend/README.md) ile bir interface implement etmiş bir tipiniz varsa, onu da sorunsuzca serialize edebilirsiniz — JSON görünümü sadece **veri** (property'ler) içerir, davranış (metotlar, event'ler) doğal olarak JSON'a yansımaz.

### 5. Standart `JsonSerializerOptions` desteği
`PropertyNamingPolicy` (camelCase gibi), `PropertyNameCaseInsensitive` gibi standart ayarlar beklendiği gibi çalışır — kendi özel bir ayar sistemimiz yok, `System.Text.Json`'ın kendi sistemine uyuyoruz.

---

## Hızlı başlangıç

```csharp
using DSO.Core.Evoker;
using DSO.Core.Evoker.Json;

var musteri = DynamicClass.CreateClass("Musteri")
    .AddProperty<int>("Id")
    .AddProperty<string>("Ad");

musteri.SetValue<int>("Id", 42);
musteri.SetValue<string>("Ad", "Ahmet");

string json = musteri.ToJson();
// {"Id":42,"Ad":"Ahmet"}

var geriGelen = DynamicClassJsonExtensions.FromJson(musteri.Type, json);
Console.WriteLine(geriGelen.GetValue<string>("Ad")); // "Ahmet"
```

---

## Kapsamlı Özellik Rehberi

### Serialize etme

```csharp
string json = dc.ToJson();                          // varsayılan ayarlarla
string json2 = dc.ToJson(myOptions);                 // kendi JsonSerializerOptions'ınızla
```

### Deserialize etme

```csharp
// Tipi zaten biliyorsanız (statik referans veya daha önce tanımladığınız bir şema):
DynamicClass geriGelen = DynamicClassJsonExtensions.FromJson(musteri.Type, json);

// Var olan bir DynamicClass'ın ŞEMASINI şablon olarak kullanarak (kendisini DEĞİŞTİRMEZ):
DynamicClass geriGelen2 = musteri.FromJsonUsingSchemaOf(json);
```

### Naming policy (camelCase vb.)

```csharp
var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

var dc = DynamicClass.CreateClass("X").AddProperty<int>("UserId");
dc.SetValue<int>("UserId", 7);

string json = dc.ToJson(options); // {"userId":7}
var geri = DynamicClassJsonExtensions.FromJson(dc.Type, json, options); // camelCase'i geri okur
```

### İç içe (nested) dinamik tipler

```csharp
var adres = DynamicClass.CreateClass("Adres").AddProperty<string>("Sehir");
var kisi = DynamicClass.CreateClass("Kisi")
    .AddProperty<string>("Ad")
    .AddProperty("Adres", adres.Type);   // property tipi BAŞKA bir dinamik tip

// ... değerleri doldurun ...

string json = kisi.ToJson();
// {"Ad":"Ayşe","Adres":{"Sehir":"Ankara"}}  <-- otomatik iç içe, hiç ekstra kod yok
```

### `Implement<T>` ile üretilen tipler

```csharp
using DSO.Core.Evoker.Extend;

var dc = DynamicClass.CreateClass("X").Implement<ICustomer>(); // ICustomer: Id, Name property'leri + Validate() metodu
dc.SetValue<int>("Id", 1);
dc.SetMethod<Func<bool>>("Validate", () => true);

string json = dc.ToJson();
// {"Id":1,"Name":null}  <-- SADECE property'ler; Validate() metodu (davranış) JSON'a yansımaz
```

### Kendi `JsonConverterFactory`'nizle birlikte kullanma

`DynamicClassJsonConverterFactory`, standart bir `System.Text.Json.Serialization.JsonConverterFactory`'dir — kendi `JsonSerializerOptions`'ınıza diğer converter'larınızla birlikte ekleyebilirsiniz:

```csharp
var options = new JsonSerializerOptions();
options.Converters.Add(new DynamicClassJsonConverterFactory());
options.Converters.Add(new BenimOzelConverterim());
// options.Converters.Add(...) diğer converter'larınız

string json = JsonSerializer.Serialize(dc.RawInstance, dc.Type, options);
```

`dc.ToJson(options)` çağırırsanız, converter zaten eklenmemişse otomatik (idempotent) olarak eklenir — elle eklemeyi unutsanız bile çalışır.

---

## Kapsam ve Sınırlar

- Sadece `AddProperty` ile eklenen (yani `DynamicTypeFactory.GetSchema`'nın döndürdüğü) property'ler serialize edilir. Metotlar, event'ler, indexer'lar JSON'da **görünmez** — bu beklenen bir davranıştır (bir JSON belgesinde "davranış" zaten temsil edilemez).
- Bilinmeyen JSON alanları deserialize sırasında **sessizce atlanır** (hata vermez) — `System.Text.Json`'ın standart davranışıyla tutarlı.
- Şemada olmayan bir property'ye JSON'da rastlanmazsa, o property CLR tipi için varsayılan değerinde kalır (`default(T)`).

## Gereksinimler

.NET 6.0+ · [`DSO.Core.Evoker`](../DSO.Core.Evoker/README.md) (proje referansı) · Ekstra NuGet paketi **gerekmez**
