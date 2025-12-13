import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

interface FaqItem {
  category: string;
  question: string;
  answer: string;
  isOpen?: boolean;
}

@Component({
  selector: 'app-faq',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './faq.component.html',
  styleUrls: ['./faq.component.css']
})
export class FaqComponent {
  faqs: FaqItem[] = [
    // Genel
    {
      category: 'Genel',
      question: 'Kütüphane çalışma saatleri nedir?',
      answer: 'Kütüphane A Blok 7/24, B Blok ise 08.30-21.30 saatleri arasında açıktır. Kütüphane B Blok hafta sonları ve resmi tatillerde kapalıdır. (Sınav dönemlerinde kısa süreli değişiklikler olabilmektedir.)'
    },
    {
      category: 'Genel',
      question: 'Kütüphaneden kimler yararlanabilir?',
      answer: 'Kütüphanemizden; Üniversitemiz öğrenci, akademik ve idari personel ile misafir araştırmacılar yararlanabilmektedir. Misafir araştırmacılar bilgi kaynaklarını yerinde kullanabilir ödünç verilemez.'
    },
    {
      category: 'Genel',
      question: 'Kütüphane kullanıcı hesabıma nasıl erişebilirim?',
      answer: 'Web sayfasında bulunan Katalog Tarama menüsü içerisindeki Oturum Aç sekmesinden T.C. kimlik numarası/öğrenci numarası ve şifre ile giriş yapılabilir. Şifrenizi bilmiyorsanız, "şifremi unuttum"a tıklayarak devam edilebilir ya da kutuphane@sakarya.edu.tr adresine şifre talebi e-postası gönderilebilir.'
    },
    {
      category: 'Genel',
      question: 'Kütüphane sosyal medya hesabı var mı?',
      answer: 'Kütüphanemizi kendi web sitesi dışında Facebook, Twitter, Instagram hesaplarımızdan takip edebilir, sorun ve görüşlerinizi bildirebilirsiniz. Youtube kanalımızdan eğitim içerikli videolara erişebilirsiniz.'
    },

    // Katalog Tarama
    {
      category: 'Katalog Tarama',
      question: 'Kütüphane koleksiyonunun içeriğini nasıl görebilirim?',
      answer: 'Katalog üzerinden araştırma yapmak için aradığınız bilgi kaynağının eser adı, yazar adı, konu başlıkları, ISBN/ISSN vb. herhangi bir anahtar kelimesiyle arama yapabilirsiniz.'
    },
    {
      category: 'Katalog Tarama',
      question: 'Kitaplar raflara neye göre yerleştiriliyor?',
      answer: 'Kütüphanemiz, Dewey Onlu Sınıflama Sistemini kullanmaktadır. Kitaplar açık raf esasına göre konusal olarak raflara yerleştirilmiştir. Böylelikle aynı konuyu ihtiva eden kitaplar bir araya gelmesi sağlanır.'
    },
    {
      category: 'Katalog Tarama',
      question: 'Kitap ayırtma işlemi nedir, nasıl yaparım?',
      answer: 'Ayırtma işlemi yapmak için kullanıcı hesabınızdan giriş yaparak katalog taraması yapmanız gerekmektedir. İstediğiniz kitap farklı biri tarafından ödünç alınmışsa katalog bilgileri üzerinde yer alan ayırtma butonuna basarak kitabı ayırtmış olursunuz.'
    },

    // Ödünç Verme
    {
      category: 'Ödünç Verme',
      question: 'Tek seferde en fazla kaç kitap ödünç alabilirim?',
      answer: 'Lisans/Ön Lisans Öğrencileri: 10 kitap 15 gün. Lisansüstü Öğrenciler: 15 kitap 30 gün. Akademik Personel: 20 kitap 60 gün. İdari Personel: 10 kitap 30 gün. Kitaplar farklı bir kullanıcı tarafından ayırtılmadığı ve kullanıcıların borç ve cezası olmadığı takdirde iade süresi 2 kere uzatılabilir.'
    },
    {
      category: 'Ödünç Verme',
      question: 'Süre uzatma işlemini nasıl yapabilirim?',
      answer: 'Ödünç alınan kaynağın kullanım süresi, iade tarihinden önce, başka bir kullanıcı tarafından bilgi kaynağı ayırt edilmemişse ve kullanıcının cezası yoksa Ödünç Verme Bankosuna gelerek ya da iade tarihine 3 gün kala web sitesi üzerinden süre uzatma işlemlerini yapabilirler. İade tarihinin son günü süre uzatma işlemi yapılamamaktadır.'
    },
    {
      category: 'Ödünç Verme',
      question: 'Bilgisayar ödünç verme hizmetinden nasıl faydalanabilirim?',
      answer: 'Kütüphanemizde 30 gün süreyle bilgisayar ödünç verme sistemi mevcuttur. Bilgisayarlar eğitim yılı boyunca 1 kere ödünç verilmektedir ve süre uzatma işlemi yapılmamaktadır.'
    },
    {
      category: 'Ödünç Verme',
      question: 'Ödünç Alınan kitabı kütüphaneye getirmediğim takdirde bir ceza uygulanır mı?',
      answer: 'Ödünç alınan kitapların kullanım süresini aşarsa ilk on gün için 20 TL sonraki her gün için 5 TL gecikme bedeli alınır. Gecikme bedelleri Ziraat Bankası Sakarya Üniversitesi Rektörlüğü TR10 0001 0020 9429 5159 7559 12 hesabına yatırıldıktan sonra dekont Banko görevlisine verilmeli veya kutuphane@sakarya.edu.tr e-posta adresine gönderilmelidir. Açıklama kısmına Gecikme Bedeli olarak yazdırılmalıdır.'
    },

    // Tezler
    {
      category: 'Tezler',
      question: 'Tezlere nasıl erişebilirim?',
      answer: 'Kütüphanemiz koleksiyonunda yer alan basılı tezlere kütüphane giriş katındaki referans bölümünde ulaşabilirsiniz. Tezlerin elektronik nüshalarına ise kütüphane web sitesinde katalog sekmesi içinde yer alan tez kataloğundan erişebilirsiniz. Tezlerden çevrimiçi yararlanabilmek için kullanıcı hesabınızla giriş yapmanız gerekmektedir. Tezler ödünç verilmez.'
    },
    {
      category: 'Tezler',
      question: 'Türkiye genelinde yayınlamış olan tezlere nasıl erişebilirim?',
      answer: 'Farklı üniversitelerde yayınlanan tezlere YÖK Tez kataloğu aracılığıyla erişebilirsiniz. Eski tarihli tezlere, bilgi kaynaklarına erişmek için ULAKBİM – Belge sağlama sistemi üzerinden erişebilirsiniz. Sisteme giriş yapmak için üyelik açmanız gerekmektedir.'
    },

    // Süreli Yayınlar
    {
      category: 'Süreli Yayınlar',
      question: 'Süreli yayınlara nasıl erişebilirim?',
      answer: 'Koleksiyonumuzda yer alan basılı süreli yayınlara kütüphane giriş katındaki süreli yayınlar bölümündedir. Süreli yayının son sayıları güncel dergi rafından, aradığınız son yıllardaki süreli yayın değilse ya da ciltlenmiş sayı ise Süreli arşiv bölümünde ulaşabilirsiniz. Elektronik süreli yayınlara ise web sitemizde yer alan Süreli Yayın Kataloğundan erişebilirseniz.'
    },
    {
      category: 'Süreli Yayınlar',
      question: 'Süreli yayınları ödünç alabilir miyim?',
      answer: 'Süreli yayınlar, ödünç verilmez.'
    },

    // Veri Tabanları
    {
      category: 'Veri Tabanları',
      question: 'Kütüphanenin abone olduğu veri tabanlarına nasıl erişebilirim?',
      answer: 'Kütüphanemizin abone olduğu veri tabanlarına kütüphane web sitesinde yer alan E-Kaynaklar sekmesinde bulunan Abone Olunan Veri Tabanları kısmından erişebilirsiniz.'
    },
    {
      category: 'Veri Tabanları',
      question: 'Veri tabanlarına kampüs dışından erişebilir miyim?',
      answer: 'Kampüs dışından veri tabanlarına VETİS sistemi üzerinden erişebilirsiniz. Detaylı bilgi için kütüphane web sitesinde yer alan Kampüs Dışı Erişim Rehberini inceleyebilirsiniz.'
    },

    // Eğitim
    {
      category: 'Eğitim',
      question: 'Kütüphanenin kullanıcılara yönelik eğitim programları mevcut mu?',
      answer: 'Kütüphanemiz kullanıcı istek ve ihtiyaçları doğrultusunda eğitimler düzenlemektedir. Oryantasyon ve kullanıcı eğitimi şeklinde verilen eğitimler hakkında daha fazla bilgiye kütüphane web sitesinde yer alan Eğitim sekmesinden ulaşabilirsiniz.'
    },
    {
      category: 'Eğitim',
      question: 'Elektronik bilgi kaynaklarının kullanımına yönelik eğitim talep edebilir miyim?',
      answer: 'Elektronik bilgi kaynaklarının kullanımı hakkında eğitim talep edebilirsiniz. Talebinizi bize iletmek için kütüphane web sitesinde yer alan eğitim sekmesindeki Eğitim Talep Formunu doldurarak kutuphane@sakarya.edu.tr adresine e-posta gönderebilirsiniz.'
    }
  ];

  categories: string[] = [];

  constructor() {
    // Kategorileri çıkar
    this.categories = [...new Set(this.faqs.map(faq => faq.category))];
  }

  toggleFaq(faq: FaqItem) {
    faq.isOpen = !faq.isOpen;
  }

  getFaqsByCategory(category: string): FaqItem[] {
    return this.faqs.filter(faq => faq.category === category);
  }
}
