// Qalam - register scoring for concordance lines.
//
// Open subtitle corpora carry no register tag: a line from a comedy and a line
// from a courtroom scene look identical to the index. Reverso's advantage is
// editorial ordering, so this file supplies the missing signal locally.
//
// Two sources of evidence, both computed from the line itself:
//   * a per-language lexicon of colloquial markers (particles, address terms,
//     intensifiers, interjections, profanity, dialect forms) and of formal
//     markers (polite pronouns, honorifics, official phrasing);
//   * structural cues that distinguish spoken dialogue from written prose.
//
// Markers are written in their normal spelling and folded through the same
// normalizer the packs use, so "coño" and "cono", "чёрт" and "чорт" match the
// same way. Matching is token based for spaced scripts and substring based for
// Chinese and Japanese, which have no spaces to tokenize on.
//
// This is a ranking signal, not a truth claim: a line with no marker scores
// zero and simply keeps its corpus order.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Qalam
{
    internal sealed class RegisterLexicon
    {
        public readonly HashSet<string> Words = new HashSet<string>(StringComparer.Ordinal);
        public readonly List<string[]> Phrases = new List<string[]>();
        public readonly List<string> Fragments = new List<string>();

        public readonly HashSet<string> FormalWords = new HashSet<string>(StringComparer.Ordinal);
        public readonly List<string[]> FormalPhrases = new List<string[]>();
        public readonly List<string> FormalFragments = new List<string>();

        public RegisterLexicon Add(params string[] markers)
        {
            foreach (string marker in markers) AddOne(marker, false);
            return this;
        }

        public RegisterLexicon AddFragment(params string[] markers)
        {
            foreach (string marker in markers) AddOne(marker, true);
            return this;
        }

        public RegisterLexicon Formal(params string[] markers)
        {
            foreach (string marker in markers)
            {
                string folded = Register.Fold(marker);
                if (folded.Length == 0) continue;
                string[] parts = folded.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;
                if (parts.Length == 1) FormalWords.Add(parts[0]);
                else FormalPhrases.Add(parts);
            }
            return this;
        }

        public RegisterLexicon FormalFragment(params string[] markers)
        {
            foreach (string marker in markers)
            {
                string folded = Register.Fold(marker);
                if (folded.Length > 1) FormalFragments.Add(folded);
            }
            return this;
        }

        private void AddOne(string marker, bool fragment)
        {
            if (string.IsNullOrWhiteSpace(marker)) return;
            string folded = Register.Fold(marker);
            if (folded.Length == 0) return;
            if (fragment)
            {
                if (!Fragments.Contains(folded)) Fragments.Add(folded);
                return;
            }
            string[] parts = folded.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;
            if (parts.Length == 1) Words.Add(parts[0]);
            else Phrases.Add(parts);
        }
    }

    internal static class Register
    {
        // Strongest evidence wins, so the sources are additive but saturating:
        // one marker already moves a line above a flat one, five do not let it
        // outrank an exact phrase match.
        private const double MarkerWeight = 0.25;
        private const double MarkerCap = 0.70;
        private const double FormalWeight = 0.30;
        private const double FormalCap = 0.65;
        private const double CuratedBonus = 0.30;

        // Below the 100 points a single extra matched token earns, above the 30
        // points a well-sized sentence earns. Exact matches (500) stay on top.
        private const double RankWeight = 70.0;

        private static readonly Dictionary<string, RegisterLexicon> Lexicons = BuildLexicons();

        public static string Fold(string value)
        {
            return ConcordanceText.Normalize(value ?? "");
        }

        public static RegisterLexicon LexiconFor(string language)
        {
            string code = (language ?? "").Trim().ToLowerInvariant().Replace('-', '_');
            if (code.Length == 0) return null;

            RegisterLexicon lexicon;
            if (Lexicons.TryGetValue(code, out lexicon)) return lexicon;
            // Script variants fall back to the script they are written in rather
            // than to a generic "zh" entry that no pack actually uses.
            if (code.StartsWith("zh_tw", StringComparison.Ordinal) ||
                code.StartsWith("zh_hk", StringComparison.Ordinal))
                return Lexicons.TryGetValue("zh_tw", out lexicon) ? lexicon : null;
            if (code.StartsWith("zh", StringComparison.Ordinal))
                return Lexicons.TryGetValue("zh_cn", out lexicon) ? lexicon : null;

            int cut = code.IndexOf('_');
            if (cut > 0)
            {
                code = code.Substring(0, cut);
                if (Lexicons.TryGetValue(code, out lexicon)) return lexicon;
            }
            return null;
        }

        public static double Value(string source, string target, string sourceLang, string targetLang)
        {
            return Value(source, target, sourceLang, targetLang, null);
        }

        // exclude holds the query's own tokens. A line is not colloquial because
        // it contains the word the user typed; the evidence has to be in the rest
        // of the sentence, otherwise every hit scores and ordering becomes noise.
        public static double Value(string source, string target, string sourceLang, string targetLang,
                                   HashSet<string> exclude)
        {
            double sourceSide = Side(source, sourceLang, exclude);
            double targetSide = Side(target, targetLang, exclude);
            // A colloquial line on either side is enough; both sides agreeing is
            // better evidence than one, but must not double count.
            return Math.Min(1.0, Math.Max(sourceSide, targetSide) + 0.2 * Math.Min(sourceSide, targetSide));
        }

        // Re-score only the lines that already won on lexical match, so a query
        // against a two-million-row pack normalizes a few dozen rows, not 25000.
        public static void Rerank(List<ConcordanceHit> hits, int max, string sourceLang, string targetLang,
                                  List<string> queryTokens)
        {
            if (hits == null || hits.Count == 0) return;
            HashSet<string> exclude = null;
            if (queryTokens != null && queryTokens.Count > 0)
                exclude = new HashSet<string>(queryTokens, StringComparer.Ordinal);
            int window = Math.Min(hits.Count, Math.Max(max * 8, 24));
            for (int i = 0; i < window; i++)
            {
                ConcordanceHit hit = hits[i];
                if (hit.Pair == null) continue;
                double value = Value(hit.Pair.Source, hit.Pair.Target, sourceLang, targetLang, exclude);
                hit.Register = value;
                hit.Score += RankWeight * value;
            }

            // Only the rescored head can move: everything below it had a lower
            // base score and gained nothing, so sorting the head is enough.
            List<ConcordanceHit> head = hits.GetRange(0, window);
            head.Sort(delegate(ConcordanceHit a, ConcordanceHit b)
            {
                int compare = b.Score.CompareTo(a.Score);
                if (compare != 0) return compare;
                return a.Pair.Source.Length.CompareTo(b.Pair.Source.Length);
            });
            hits.RemoveRange(0, window);
            hits.InsertRange(0, head);
        }

        private static double Side(string text, string language, HashSet<string> exclude)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            string norm = ConcordanceText.Normalize(text);
            if (norm.Length == 0) return 0;

            string[] words = norm.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return 0;

            double value = 0;
            int evidence = 0;
            RegisterLexicon lexicon = LexiconFor(language);
            if (lexicon != null)
            {
                int markers = CountHits(words, norm, lexicon.Words, lexicon.Phrases, lexicon.Fragments,
                                        exclude);
                if (markers > 0)
                {
                    value += Math.Min(MarkerCap, MarkerWeight * markers);
                    evidence += markers;
                }

                int formal = CountHits(words, norm, lexicon.FormalWords, lexicon.FormalPhrases,
                                        lexicon.FormalFragments, exclude);
                if (formal > 0) value -= Math.Min(FormalCap, FormalWeight * formal);
            }

            // The curated phrasebook is hand-checked slang, so a hit there is the
            // strongest single piece of evidence available offline.
            string code = (language ?? "").Trim().ToLowerInvariant();
            if (code == "es" || code == "en")
            {
                try
                {
                    if (Colloquial.FindMatches(text, code).Count > 0)
                    {
                        value += CuratedBonus;
                        evidence++;
                    }
                }
                catch { }
            }

            // Structure can only amplify evidence, never manufacture it. Without
            // this a short flat line scored 0.07 and every hit was "boosted",
            // which tinted the whole result set instead of ordering it.
            if (evidence == 0) return 0;

            if (words.Length >= 2 && words.Length <= 7) value += 0.04;
            else if (words.Length >= 20) value -= 0.10;

            if (HasHesitation(words)) value += 0.10;
            if (text.IndexOf('!') >= 0 || text.IndexOf('\u2026') >= 0 ||
                text.IndexOf("...", StringComparison.Ordinal) >= 0) value += 0.03;

            if (value < 0) return 0;
            return value > 1.0 ? 1.0 : value;
        }

        // Distinct markers, not occurrences: "no, no, no" is one piece of evidence.
        private static int CountHits(string[] words, string norm, HashSet<string> set,
                                     List<string[]> phrases, List<string> fragments,
                                     HashSet<string> exclude)
        {
            HashSet<string> matched = new HashSet<string>(StringComparer.Ordinal);
            if (set.Count > 0)
            {
                for (int i = 0; i < words.Length; i++)
                {
                    if (!set.Contains(words[i])) continue;
                    if (exclude != null && exclude.Contains(words[i])) continue;
                    matched.Add(words[i]);
                }
            }
            if (phrases.Count > 0)
            {
                for (int p = 0; p < phrases.Count; p++)
                {
                    string[] phrase = phrases[p];
                    if (phrase.Length == 0 || phrase.Length > words.Length) continue;
                    if (AllExcluded(phrase, exclude)) continue;
                    for (int start = 0; start + phrase.Length <= words.Length; start++)
                    {
                        bool match = true;
                        for (int k = 0; k < phrase.Length; k++)
                        {
                            if (!string.Equals(words[start + k], phrase[k], StringComparison.Ordinal))
                            {
                                match = false;
                                break;
                            }
                        }
                        if (match)
                        {
                            matched.Add(string.Join(" ", phrase));
                            break;
                        }
                    }
                }
            }
            if (fragments.Count > 0)
            {
                for (int i = 0; i < fragments.Count; i++)
                {
                    if (norm.IndexOf(fragments[i], StringComparison.Ordinal) < 0) continue;
                    if (exclude != null && exclude.Contains(fragments[i])) continue;
                    matched.Add(fragments[i]);
                }
            }
            return matched.Count;
        }

        private static bool AllExcluded(string[] phrase, HashSet<string> exclude)
        {
            if (exclude == null) return false;
            for (int i = 0; i < phrase.Length; i++)
                if (!exclude.Contains(phrase[i])) return false;
            return true;
        }

        // "I-I don't know" and "no, no, no" are spoken-dialogue shapes that prose
        // almost never has. Normalization leaves the repeated token behind.
        private static bool HasHesitation(string[] words)
        {
            for (int i = 0; i + 1 < words.Length; i++)
            {
                if (words[i].Length != 1) continue;
                if (string.Equals(words[i], words[i + 1], StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static Dictionary<string, RegisterLexicon> BuildLexicons()
        {
            Dictionary<string, RegisterLexicon> map =
                new Dictionary<string, RegisterLexicon>(StringComparer.Ordinal);

            map["en"] = new RegisterLexicon()
                .Add("yeah", "yep", "nope", "nah", "gonna", "wanna", "gotta", "kinda", "sorta",
                     "dunno", "lemme", "gimme", "outta", "lotta", "ain't", "y'all", "cos", "cuz",
                     "hey", "hi", "mate", "dude", "man", "bro", "buddy", "pal", "guys", "folks",
                     "damn", "hell", "shit", "crap", "bloody", "freaking", "screw", "whatever",
                     "alright", "okay", "wow", "oops", "huh", "hmm", "um", "uh", "eh", "ugh",
                     "jerk", "idiot", "bastard", "bitch", "ass", "asshole", "moron", "crazy",
                     "cool", "awesome", "weird", "sucks", "freaking", "listen", "look", "sure",
                     "right", "anyway", "jeez", "geez", "gosh", "whoa", "nope", "yikes",
                     "shut up", "come on", "get out", "no way", "oh my god", "for god's sake",
                     "what the hell", "you know", "i mean", "kind of", "sort of", "or something")
                .Formal("sir", "madam", "ma'am", "indeed", "furthermore", "moreover", "therefore",
                        "however", "regarding", "pursuant", "shall", "would you kindly", "if you please",
                        "i would like to", "excuse me", "good evening", "may i", "allow me");

            map["es"] = new RegisterLexicon()
                .Add("joder", "mierda", "coño", "hostia", "ostia", "cabrón", "cabron", "tío",
                     "chaval", "chavala", "guay", "vale", "venga", "oye", "mira", "vamos", "anda",
                     "jope", "vaya", "menudo", "gilipollas", "imbécil", "idiota", "estúpido", "tonto",
                     "carajo", "cojones", "huevos", "leche", "cagüen", "ostras", "flipar", "mola",
                     "curro", "pasta", "colega", "chachi", "ojalá", "pues", "bueno", "hombre",
                     "chingón", "chingon", "chido", "güey", "guey", "pinche", "órale", "orale",
                     "ándale", "andale", "chale", "naco", "neta", "boludo", "pibe", "che", "vos",
                     "qué fuerte", "no me jodas", "de puta madre", "la hostia", "por dios",
                     "dios mío", "madre mía", "a huevo", "de verdad", "en serio", "o sea")
                .Formal("usted", "ustedes", "señor", "señora", "señorita", "disculpe", "disculpen",
                        "por favor", "le ruego", "tenga la bondad", "sería tan amable", "cordialmente",
                        "atentamente", "buenas tardes", "buenos días", "permítame");

            map["ar"] = new RegisterLexicon()
                .Add("يعني", "خلاص", "شو", "إيش", "ايش", "وين", "كيفك", "حبيبي", "حبيبتي", "زلمة",
                     "كتير", "هلق", "هلأ", "منيح", "بدي", "بدك", "رح", "يلا", "عنجد", "طيب",
                     "تمام", "ماشي", "زين", "هيك", "لك", "شو هاد", "شو رايك", "ولا يهمك", "يا رجل",
                     "والله", "ياخي", "يا خي", "مو", "مش", "هبل", "خرية", "كس", "شرموط", "حيوان",
                     "غبي", "مجنون", "شو القصة", "على راسي", "ما في", "ما بدي",
                     "هسي", "زول", "كيفنك", "بخلص", "أخلص", "شنو",
                     "هسة", "شكو", "ماكو", "خوش", "هواية", "دحين", "إيش", "أبغى", "الحين",
                     "عايز", "أوي", "هلق", "بدي", "عنجد", "برشا", "باهي", "واش", "هلبا", "نبي",
                     "شكون", "علاش", "بغيت", "شحال", "زحمة", "ما أدري")
                .Formal("حضرتك", "سيادتك", "الرجاء", "يرجى", "تفضلوا", "من فضلكم", "أرجو", "ارجو",
                        "السيد", "السيدة", "المحترم", "بناء على", "وفقا ل", "نود أن", "يرجى التكرم");

            map["fr"] = new RegisterLexicon()
                .Add("putain", "merde", "bordel", "con", "connard", "salaud", "mec", "meuf", "gars",
                     "truc", "machin", "ouais", "nan", "ben", "bah", "hein", "genre", "grave",
                     "trop", "vachement", "carrément", "chiant", "foutu", "bouffer", "bosser",
                     "kiffer", "galère", "nickel", "sympa", "dingue", "gueule", "nana", "pote",
                     "boulet", "nul", "cool", "bosser", "la vache", "oh là là", "fais chier",
                     "chais pas", "tu sais", "tu vois", "mon vieux", "ma vieille", "putain de")
                .Formal("vous", "monsieur", "madame", "mademoiselle", "veuillez", "afin", "ainsi",
                        "toutefois", "néanmoins", "par conséquent", "je vous prie", "auriez-vous",
                        "s'il vous plaît", "excusez-moi", "bonsoir", "permettez");

            map["de"] = new RegisterLexicon()
                .Add("scheiße", "scheisse", "verdammt", "mist", "krass", "geil", "alter", "typ",
                     "kerl", "junge", "tja", "ach", "ey", "boah", "echt", "voll", "total", "hammer",
                     "bock", "schnauze", "halt", "quatsch", "blöd", "doof", "idiot", "penner",
                     "wichser", "ficken", "arsch", "arschloch", "blödsinn", "spinnst", "verarscht",
                     "was soll das", "ach so", "na ja", "mein gott", "um gottes willen", "keine ahnung",
                     "hast du", "weißt du", "weisst du", "komm schon")
                .Formal("sie", "ihnen", "bitte", "danke", "entschuldigung", "guten tag", "guten abend",
                        "möchten", "könnten", "würden", "sehr geehrte", "gestatten", "erlauben sie",
                        "ich möchte sie bitten");

            map["it"] = new RegisterLexicon()
                .Add("cazzo", "merda", "minchia", "cavolo", "porca", "puttana", "stronzo", "cretino",
                     "idiota", "scemo", "raga", "tipo", "tizio", "roba", "casino", "figata", "dai",
                     "boh", "beh", "mah", "ecco", "cioè", "insomma", "magari", "mica", "proprio",
                     "davvero", "accidenti", "diamine", "madonna", "vaffanculo", "coglione", "sfigato",
                     "che palle", "porca miseria", "non lo so", "ma dai", "figurati", "roba da matti")
                .Formal("lei", "signore", "signora", "signorina", "scusi", "prego", "gentilmente",
                        "vorrei", "dovrebbe", "cortesemente", "la prego", "buongiorno", "buonasera",
                        "mi scusi", "le chiedo");

            map["pt"] = new RegisterLexicon()
                .Add("porra", "caralho", "merda", "bosta", "puta", "foda", "cacete", "cara", "mano",
                     "bicho", "moleque", "garoto", "bagulho", "parada", "legal", "massa", "maneiro",
                     "chato", "saco", "droga", "nossa", "caramba", "credo", "eita", "oxe", "uai",
                     "sinistro", "tipo", "véi", "putz", "vixe", "pra", "tá", "né", "pô",
                     "que isso", "sei lá", "não sei", "meu deus", "nossa senhora", "a gente")
                .Formal("senhor", "senhora", "o senhor", "a senhora", "gostaria", "poderia",
                        "gentileza", "por favor", "prezado", "prezada", "atenciosamente", "com licença",
                        "queira", "solicito");

            map["ru"] = new RegisterLexicon()
                .Add("блин", "чёрт", "черт", "хрен", "фиг", "капец", "пипец", "жесть", "короче",
                     "типа", "ваще", "офигеть", "охренеть", "достал", "задолбал", "дурак", "идиот",
                     "придурок", "козёл", "козел", "сволочь", "гад", "чувак", "пацан", "мужик",
                     "баба", "тёлка", "телка", "халява", "лажа", "облом", "ништяк", "класс", "круто",
                     "супер", "давай", "ладно", "ага", "щас", "чё", "че", "что ли", "как бы",
                     "вот блин", "господи", "боже мой", "ну и")
                .Formal("вы", "пожалуйста", "извините", "здравствуйте", "уважаемый", "уважаемая",
                        "следовательно", "однако", "в соответствии", "разрешите", "позвольте",
                        "будьте добры", "добрый день");

            map["nl"] = new RegisterLexicon()
                .Add("kut", "godverdomme", "verdomme", "klote", "lul", "tyfus", "gast", "gozer",
                     "kerel", "meid", "joh", "nou", "effe", "gewoon", "echt", "hartstikke", "gaaf",
                     "tof", "maf", "lullo", "eikel", "klootzak", "verrek", "jezus", "mens", "kijk",
                     "kom op", "doe normaal", "geen idee", "weet ik veel", "wat nou", "nou ja",
                     "godver", "verdorie", "sjonge")
                .Formal("u", "meneer", "mevrouw", "excuseert", "alstublieft", "zou u", "graag",
                        "mag ik", "neemt u", "goedemiddag", "goedenavond", "met uw verlof");

            map["pl"] = new RegisterLexicon()
                .Add("kurwa", "cholera", "gówno", "chuj", "pizda", "jebać", "spierdalaj", "debil",
                     "idiota", "kretyn", "baran", "facet", "chłop", "koleś", "ziom", "laska",
                     "dziewczyna", "serio", "naprawdę", "kurde", "kurczę", "masakra", "odjazd",
                     "spoko", "luz", "bzdura", "głupio", "no nie", "do cholery", "jasna cholera",
                     "o boże", "matko", "chyba nie", "nie wiem")
                .Formal("pan", "pani", "proszę", "przepraszam", "uprzejmie", "szanowny", "szanowna",
                        "czy mógłby", "pozwoli pan", "dzień dobry", "dobry wieczór");

            map["tr"] = new RegisterLexicon()
                .Add("lan", "lanet", "siktir", "orospu", "şerefsiz", "gerizekalı", "salak", "aptal",
                     "manyak", "deli", "abi", "abla", "kanka", "moruk", "usta", "hacı", "yani",
                     "işte", "hani", "hadi", "tamam", "bok", "vallahi", "harbi", "ciddi", "fena",
                     "süper", "harika", "ya", "be", "lanet olsun", "allahaşkına", "ne var",
                     "ne oldu", "bilmiyorum", "saçmalama")
                .Formal("efendim", "beyefendi", "hanımefendi", "lütfen", "özür", "rica", "saygıdeğer",
                        "müsaade eder", "izin verir", "günaydın", "iyi akşamlar");

            map["he"] = new RegisterLexicon()
                .Add("סבבה", "יאללה", "וואלה", "דוגרי", "חארטה", "בלאגן", "פאדיחה", "מגניב", "כיף",
                     "נו", "כאילו", "סתם", "ממש", "בטח", "באמת", "לעזאזל", "מטומטם", "אידיוט",
                     "מפגר", "חרא", "זין", "תזדיין", "מה נשמע", "בחייך", "איזה באסה", "שמע",
                     "תשמע", "יא", "אחי", "אחותי")
                .Formal("אדוני", "גברתי", "בבקשה", "סליחה", "נא", "בכבוד", "אני מבקש", "הרשה לי",
                        "בוקר טוב", "ערב טוב");

            map["vi"] = new RegisterLexicon()
                .Add("trời", "trời ơi", "chết", "khỉ", "mẹ", "đéo", "địt", "vãi", "vkl", "vcc",
                     "chó", "thằng", "mày", "tao", "tớ", "ấy", "nhé", "nha", "thôi", "thế", "vậy",
                     "ừ", "ờ", "hả", "gì", "sao", "rồi", "luôn", "ghê", "sướng", "vcl", "điên",
                     "khùng", "ngu", "thật à", "không biết")
                .Formal("quý vị", "thưa", "kính", "xin", "vui lòng", "ngài", "trân trọng",
                        "kính thưa", "xin chào quý");

            map["ko"] = new RegisterLexicon()
                .Add("야", "아이씨", "씨발", "미친", "대박", "진짜", "완전", "존나", "짱", "헐",
                     "그니까", "뭐", "좀", "너", "니", "우리", "왜", "미쳤어", "죽을래", "꺼져",
                     "닥쳐", "빨리", "잠깐", "아이고", "어머", "대체", "뭐야", "말도 안 돼")
                .Formal("습니다", "입니다", "하십시오", "십시오", "하세요", "세요", "께서", "드립니다",
                        "감사합니다", "죄송합니다", "실례합니다", "안녕하십니까")
                .FormalFragment("습니다", "입니다", "십시오", "하세요", "드립니다", "안녕하십니까");

            map["zh_cn"] = new RegisterLexicon()
                .AddFragment("妈的", "他妈", "傻逼", "混蛋", "神经病", "搞什么", "干嘛", "咋", "啥",
                             "呗", "嘛", "哎", "哎呀", "哎哟", "天哪", "我的天", "算了", "得了",
                             "牛逼", "厉害", "烦死", "气死", "滚", "闭嘴", "咱", "哥们", "兄弟",
                             "老兄", "妹子", "美女", "帅哥", "咋办", "咋了", "靠", "操", "草",
                             "别闹", "你说呢", "受不了", "搞什么鬼")
                .Formal("您", "请", "抱歉", "对不起", "先生", "女士", "尊敬", "请问", "谢谢您", "劳驾")
                .FormalFragment("您", "请问", "劳驾");

            map["zh_tw"] = new RegisterLexicon()
                .AddFragment("媽的", "他媽", "傻B", "混蛋", "神經病", "搞什麼", "幹嘛", "為啥", "啦",
                             "喔", "欸", "唉唷", "天啊", "我的天", "算了", "好啦", "厲害", "煩死",
                             "滾", "閉嘴", "咱們", "兄弟", "妹子", "正妹", "帥哥", "怎麼辦", "怎樣",
                             "靠", "操", "別鬧", "受不了", "搞什麼鬼")
                .Formal("您", "請", "抱歉", "對不起", "先生", "女士", "尊敬", "請問", "謝謝您", "勞駕")
                .FormalFragment("您", "請問", "勞駕");

            map["ja"] = new RegisterLexicon()
                .AddFragment("やばい", "すげえ", "すごい", "まじ", "マジ", "うそ", "やだ", "だめ",
                             "ばか", "バカ", "あほ", "くそ", "ちくしょう", "しね", "うるさい",
                             "だまれ", "ちょっと", "ねえ", "おい", "あの", "うん", "いや", "なんか",
                             "だって", "じゃあ", "そっか", "まあ", "ほんと", "ごめん", "ありがと",
                             "おまえ", "てめえ", "こいつ", "あいつ", "やつ", "めっちゃ", "ちょー",
                             "超", "だよ", "だね", "だろ", "じゃん", "かよ", "だぜ", "だぞ", "のかよ")
                .Formal("です", "ます", "ございます", "ください", "様", "誠に", "恐れ入ります",
                        "いらっしゃ", "いたします")
                .FormalFragment("です", "ます", "ございます", "ください", "いらっしゃ", "いたします");

            map["hi"] = new RegisterLexicon()
                .Add("यार", "अरे", "अबे", "ओए", "भाई", "साला", "कमीना", "बकवास", "चूतिया", "हरामी",
                     "पगला", "पागल", "बेवकूफ", "अजीब", "मस्त", "बढ़िया", "झकास", "टाइप", "बंदा",
                     "लड़की", "क्या", "क्यों", "चल", "हट", "बक्कल", "यार बकवास", "क्या बात है",
                     "मज़ा आया", "नहीं पता")
                .Formal("आप", "कृपया", "धन्यवाद", "क्षमा", "महोदय", "श्रीमती", "श्रीमान", "अनुरोध",
                        "कृपया प्रतीक्षा", "सादर");

            map["uk"] = new RegisterLexicon()
                .Add("блін", "чорт", "дідько", "хрін", "фіг", "капець", "піпець", "жесть", "коротше",
                     "типу", "взагалі", "офігеть", "дістав", "дурень", "ідіот", "придурок", "козел",
                     "чувак", "пацан", "мужик", "баба", "клас", "круто", "супер", "давай", "гаразд",
                     "ага", "щас", "шо", "чи шо", "от блін", "боже мій", "та ну")
                .Formal("ви", "шановний", "шановна", "будь ласка", "вибачте", "добрий день",
                        "дозвольте", "прошу", "з повагою");

            map["ro"] = new RegisterLexicon()
                .Add("fir-ar", "dracu", "naiba", "pula", "căcat", "rahat", "prost", "prostule",
                     "idiot", "tâmpit", "bou", "fraiere", "băi", "mă", "frate", "vere", "tip",
                     "tipă", "gagiu", "mișto", "tare", "nasol", "naspa", "hai", "gata", "așa",
                     "păi", "mă rog", "ce naiba", "la naiba", "nu știu", "las-o")
                .Formal("dumneavoastră", "domnule", "doamnă", "vă rog", "scuzați", "respectuos",
                        "permiteți-mi", "bună ziua", "bună seara");

            return map;
        }
    }

    internal static class RegisterSelfTest
    {
        private sealed class Case
        {
            public string Lang;
            public string Colloquial;
            public string Flat;
        }

        public static int Run(string[] args)
        {
            string output = Path.Combine(Paths.AppDir, "registertest.txt");
            for (int i = 0; i + 1 < args.Length; i++)
                if (string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase))
                    output = Path.GetFullPath(args[i + 1]);

            List<string> lines = new List<string>();
            List<Case> cases = BuildCases();
            int failures = 0;
            lines.Add("register ranking self-test");
            lines.Add("");

            foreach (Case item in cases)
            {
                double colloquial = Register.Value(item.Colloquial, null, item.Lang, "en");
                double flat = Register.Value(item.Flat, null, item.Lang, "en");
                bool pass = colloquial > flat + 0.12 && colloquial >= 0.25;
                if (!pass) failures++;
                lines.Add(string.Format("{0}  {1,-6} slang={2:0.00} flat={3:0.00}  {4}",
                                        pass ? "PASS" : "FAIL", item.Lang, colloquial, flat,
                                        Shorten(item.Colloquial)));
            }

            // A line with no marker at all must not be promoted by structure alone.
            double neutral = Register.Value("The committee will consider the proposal tomorrow.",
                                            "Der Ausschuss wird den Vorschlag morgen prüfen.",
                                            "en", "de");
            bool neutralPass = neutral < 0.25;
            if (!neutralPass) failures++;
            lines.Add(string.Format("{0}  neutral en     value={1:0.00}",
                                    neutralPass ? "PASS" : "FAIL", neutral));

            // CJK word counting: a character is a unit, Latin stays one word.
            int zhShort = ConcordanceText.WordCountClean("我来了");
            int zhLine = ConcordanceText.WordCountClean("你他妈疯了吗");
            int jaShort = ConcordanceText.WordCountClean("やばい");
            int mixed = ConcordanceText.WordCountClean("OK我来了");
            int latin = ConcordanceText.WordCountClean("hello there");
            int korean = ConcordanceText.WordCountClean("기다려 주십시오");
            bool countPass = zhShort == 3 && zhLine == 6 && jaShort == 3 &&
                             mixed == 4 && latin == 2 && korean == 2 &&
                             ConcordanceText.WordLimit(24, "我来了") == 60 &&
                             ConcordanceText.WordLimit(24, "hello there") == 24;
            if (!countPass) failures++;
            lines.Add(string.Format("{0}  cjk counts     zh={1} ja={2} mixed={3} latin={4} ko={5}",
                                    countPass ? "PASS" : "FAIL", zhShort, jaShort, mixed, latin, korean));

            // The query's own word must not count as evidence for itself.
            HashSet<string> queryEcho = new HashSet<string>(StringComparer.Ordinal) { "whatever" };
            double echo = Register.Value("WHATEVER'S THE MATTER WITH YOUR AUNT?", null,
                                         "en", "es", queryEcho);
            bool echoPass = echo < 0.20;
            if (!echoPass) failures++;
            lines.Add(string.Format("{0}  query echo     value={1:0.00}",
                                    echoPass ? "PASS" : "FAIL", echo));

            double formal = Register.Value("Veuillez patienter ici, monsieur.",
                                           "Please wait here, sir.", "fr", "en");
            bool formalPass = formal < 0.20;
            if (!formalPass) failures++;
            lines.Add(string.Format("{0}  formal fr      value={1:0.00}",
                                    formalPass ? "PASS" : "FAIL", formal));

            if (args.Any(a => string.Equals(a, "--pack", StringComparison.OrdinalIgnoreCase)))
                Probe(lines);

            lines.Add("");
            lines.Add(failures == 0 ? "OK" : "FAILED " + failures);
            Write(output, lines);
            return failures == 0 ? 0 : 1;
        }

        private static void Write(string path, List<string> lines)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllLines(path, lines.ToArray(), new UTF8Encoding(false));
            }
            catch { }
            try
            {
                foreach (string line in lines) Console.WriteLine(line);
            }
            catch { }
        }

        // Real-pack probe: the unit cases above prove the scorer, not that it
        // changes what a query returns. This prints register values for actual
        // pack hits so a flat top result is visible rather than assumed.
        private static void Probe(List<string> lines)
        {
            string[][] queries = new string[][]
            {
                new string[] { "es", "en", "vale" },
                new string[] { "es", "en", "tio" },
                new string[] { "es", "en", "joder" },
                new string[] { "es", "en", "que pasa" },
                new string[] { "en", "es", "yeah" },
                new string[] { "en", "es", "whatever" },
                new string[] { "ar", "en", "يعني" },
                new string[] { "ar", "en", "شو" },
                new string[] { "en", "ar", "dude" }
            };

            lines.Add("");
            lines.Add("pack probe");
            int boosted = 0;
            int total = 0;
            foreach (string[] query in queries)
            {
                List<ExamplePair> results;
                try
                {
                    results = ConcordanceStore.Shared.Search(query[2], query[0], query[1], 5, null);
                }
                catch (Exception error)
                {
                    lines.Add("  " + query[0] + "->" + query[1] + " " + query[2] + " failed: " + error.Message);
                    continue;
                }

                lines.Add("");
                lines.Add("-- " + query[0] + "->" + query[1] + "  " + query[2] + "  (" + results.Count + " hits)");
                foreach (ExamplePair pair in results)
                {
                    total++;
                    if (pair.Register >= 0.25) boosted++;
                    lines.Add(string.Format("   r={0:0.00}  {1}  ||  {2}",
                                            pair.Register, Trim(pair.Source, 56), Trim(pair.Target, 56)));
                }
            }
            lines.Add("");
            lines.Add(string.Format("boosted {0}/{1} pack hits", boosted, total));
        }

        private static string Trim(string value, int width)
        {
            if (string.IsNullOrEmpty(value)) return "";
            string flat = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return flat.Length <= width ? flat : flat.Substring(0, width - 3) + "...";
        }

        private static string Shorten(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Length <= 46 ? value : value.Substring(0, 43) + "...";
        }

        private static List<Case> BuildCases()
        {
            List<Case> cases = new List<Case>();
            cases.Add(new Case { Lang = "en", Colloquial = "Yeah, whatever, man.", Flat = "I would appreciate it if you would wait here." });
            cases.Add(new Case { Lang = "es", Colloquial = "¡Joder, tío, qué guay!", Flat = "Señor, ¿podría usted esperar aquí?" });
            cases.Add(new Case { Lang = "ar", Colloquial = "يعني خلاص، شو بدي أعمل؟", Flat = "حضرتك، الرجاء الانتظار هنا." });
            cases.Add(new Case { Lang = "ar", Colloquial = "هسي الزول ده ماشي، بعد بخلص من الشغل.", Flat = "حضرتك، الرجاء الانتظار هنا." });
            cases.Add(new Case { Lang = "ar", Colloquial = "شكو ماكو، هسة نجي، هواية خوش شغل.", Flat = "حضرتك، الرجاء الانتظار هنا." });
            cases.Add(new Case { Lang = "fr", Colloquial = "Putain, mec, c'est trop bien !", Flat = "Veuillez patienter ici, monsieur." });
            cases.Add(new Case { Lang = "de", Colloquial = "Scheiße, Alter, das ist echt krass!", Flat = "Bitte warten Sie hier, meine Dame." });
            cases.Add(new Case { Lang = "it", Colloquial = "Cazzo, raga, che figata!", Flat = "La prego di attendere qui, signore." });
            cases.Add(new Case { Lang = "pt", Colloquial = "Porra, cara, que bagulho massa!", Flat = "O senhor poderia aguardar aqui, por favor." });
            cases.Add(new Case { Lang = "ru", Colloquial = "Блин, чувак, это круто!", Flat = "Пожалуйста, подождите здесь, уважаемый." });
            cases.Add(new Case { Lang = "nl", Colloquial = "Kut, gast, dat is echt gaaf!", Flat = "Meneer, zou u hier willen wachten, alstublieft." });
            cases.Add(new Case { Lang = "pl", Colloquial = "Kurwa, ziom, to jest spoko!", Flat = "Proszę pana, proszę tu zaczekać." });
            cases.Add(new Case { Lang = "tr", Colloquial = "Lan, kanka, harbi süper!", Flat = "Efendim, lütfen burada bekleyin." });
            cases.Add(new Case { Lang = "he", Colloquial = "סבבה, אחי, זה מגניב", Flat = "אדוני, אנא המתן כאן." });
            cases.Add(new Case { Lang = "vi", Colloquial = "Trời ơi, vãi thật!", Flat = "Xin quý vị vui lòng chờ ở đây." });
            cases.Add(new Case { Lang = "ko", Colloquial = "아이씨, 진짜 대박!", Flat = "죄송합니다만, 여기서 기다려 주십시오." });
            cases.Add(new Case { Lang = "zh_cn", Colloquial = "妈的，你干嘛呢？", Flat = "请您在这里等候。" });
            cases.Add(new Case { Lang = "zh_tw", Colloquial = "媽的，你搞什麼？", Flat = "請您在這裡等候。" });
            cases.Add(new Case { Lang = "ja", Colloquial = "やばい、まじでやだ。", Flat = "ここでお待ちください。" });
            cases.Add(new Case { Lang = "hi", Colloquial = "यार, ये तो मस्त है!", Flat = "कृपया यहाँ प्रतीक्षा करें।" });
            cases.Add(new Case { Lang = "uk", Colloquial = "Блін, чувак, це круто!", Flat = "Будь ласка, зачекайте тут, шановний." });
            cases.Add(new Case { Lang = "ro", Colloquial = "Fir-ar, frate, e mișto!", Flat = "Domnule, vă rog să așteptați aici." });
            return cases;
        }
    }
}
