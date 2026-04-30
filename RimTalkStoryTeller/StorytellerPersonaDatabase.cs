using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;
using Verse;

namespace LivingStoryteller
{
    [StaticConstructorOnStartup]
    public static class StorytellerPersonaDatabase
    {
        private static readonly StorytellerPersonaDef fallback;
        private static readonly List<StorytellerPersonaDef> storytellerPersonaDefs;
        private static readonly string path = System.IO.Path.Combine(ModOptions.ModContent.RootDir, "StorytellerPersona") + "/StorytellerPersona.xml";

        static StorytellerPersonaDatabase()
        {
            // Load from a file or string

            XDocument doc = XDocument.Load(path);
            LogManager.Log("[StorytellerPersonaDatabase] Loaded XML document with root: " + doc.Root?.Name);
            // Query specific elements

            storytellerPersonaDefs = doc.Root
                .Elements("StorytellerPersona")
                .Select(x => new StorytellerPersonaDef
                {
                    storytellerDefName = (string)x.Element("storytellerDefName"),
                    personaText = NormalizeMultiline((string)x.Element("personaText")),
                    voiceId = (string)x.Element("voiceId"),
                    accent = (string)x.Element("accent"),
                    gender = (string)x.Element("gender"),
                    voiceProviders = x.Element("voiceProviders")?
                        .Elements("li")
                        .Select(li => new VoiceProvider
                        {
                            name = (string)li.Element("name"),
                            voice = (string)li.Element("voice")
                        }).ToList() ?? new List<VoiceProvider>(),
                    emotionModifiers = ReadEmotionModifiers(x.Element("emotionModifiers")),
                    moodModifiers = ReadMoodModifiers(x.Element("moodModifiers"))
                })
                .ToList();

            fallback = storytellerPersonaDefs.Find(sp => sp.storytellerDefName == "Fallback") ?? new StorytellerPersonaDef
                {
                    storytellerDefName = "Fallback",
                    personaText = @"You are the storyteller guiding this colony. Comment on what just happened.",
                    voiceId = "default_voice",
                };

            foreach (var persona in storytellerPersonaDefs)
            {
                LogManager.Log($"[LivingStoryteller] Loaded storyteller persona: {persona.storytellerDefName} | VoiceId: {persona.voiceId} | Accent: {persona.accent} | PersonaText length: {persona.personaText?.Length ?? 0}");
                ModOptions.Settings.Storytellers.Add(persona.storytellerDefName);
            }
            LogManager.Log($"[LivingStoryteller] Loaded {storytellerPersonaDefs.Count} storyteller personas. Fallback persona: {fallback.storytellerDefName}");
        }

        public static void SaveToXml()
        {
            XElement root = new XElement("StorytellerPersonas");

            foreach (var p in storytellerPersonaDefs)
            {
                XElement personaNode = new XElement("StorytellerPersona",
                    new XElement("storytellerDefName", p.storytellerDefName),
                    new XElement("personaText", p.personaText ?? ""),
                    new XElement("voiceId", p.voiceId ?? ""),
                    new XElement("accent", p.accent ?? ""),
                    new XElement("gender", p.gender ?? "other")
                );

                // Voice Providers
                XElement vpList = new XElement("voiceProviders");
                foreach (var vp in p.voiceProviders)
                {
                    vpList.Add(
                        new XElement("li",
                            new XElement("name", vp.name),
                            new XElement("voice", vp.voice)
                        )
                    );
                }
                personaNode.Add(vpList);

                // Emotion Modifiers
                personaNode.Add(
                    new XElement("emotionModifiers",
                        new XElement("neutral", p.emotionModifiers.neutral ?? ""),
                        new XElement("tense", p.emotionModifiers.tense ?? ""),
                        new XElement("chaotic", p.emotionModifiers.chaotic ?? ""),
                        new XElement("somber", p.emotionModifiers.somber ?? "")
                    )
                );

                // Mood Modifiers
                personaNode.Add(
                    new XElement("moodModifiers",
                        new XElement("neutral", p.moodModifiers.neutral ?? ""),
                        new XElement("anxious", p.moodModifiers.anxious ?? ""),
                        new XElement("chaotic", p.moodModifiers.chaotic ?? ""),
                        new XElement("somber", p.moodModifiers.somber ?? ""),
                        new XElement("confident", p.moodModifiers.confident ?? "")
                    )
                );

                root.Add(personaNode);
            }

            XDocument doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
            doc.Save(path);
            LogManager.Log($"[StorytellerPersonaDatabase] Saved {storytellerPersonaDefs.Count} storyteller personas to XML at path: {path}");
        }
        private static EmotionModifier ReadEmotionModifiers(XElement node)
        {
            if (node == null) return new EmotionModifier();

            return new EmotionModifier
            {
                neutral = (string)node.Element("neutral"),
                tense = (string)node.Element("tense"),
                chaotic = (string)node.Element("chaotic"),
                somber = (string)node.Element("somber")
            };
        }

        private static MoodModifier ReadMoodModifiers(XElement node)
        {
            if (node == null) return new MoodModifier();

            return new MoodModifier
            {
                neutral = (string)node.Element("neutral"),
                anxious = (string)node.Element("anxious"),
                chaotic = (string)node.Element("chaotic"),
                somber = (string)node.Element("somber"),
                confident = (string)node.Element("confident")
            };
        }

        private static string NormalizeMultiline(string s)
        {
            return string.IsNullOrWhiteSpace(s)
                ? string.Empty
                : s.Trim(); // you can add more cleanup if you want
        }

        private static StorytellerPersonaDef Get(string storytellerDefName)
        {
            return storytellerPersonaDefs.Find(sp => sp.storytellerDefName == storytellerDefName) ?? fallback;
        }
        public static StorytellerPersonaDef GetPersonaDef(string storytellerDefName)
        {
            return Get(storytellerDefName);
        }

        public static string GetPersonaText(string storytellerDefName)
        {
            return GetPersonaDef(storytellerDefName)?.personaText ?? "";
        }

        public static string GetVoiceId(string storytellerDefName)
        {
            return GetPersonaDef(storytellerDefName)?.voiceId ?? "default_voice";
        }
        public static string GetAccent(string storytellerDefName)
        {
            return GetPersonaDef(storytellerDefName)?.accent ?? "";
        }

        public static string GetVoice(string storytellerDefName, string providerName)
        {
            var voiceModel = GetPersonaDef(storytellerDefName)?.voiceProviders.FirstOrDefault(vm => vm.name == providerName);
            var voice = voiceModel?.voice ?? string.Empty;
            if (string.IsNullOrEmpty(voice))
            {
                LogManager.Log($"No voice found for storyteller '{storytellerDefName}' and provider '{providerName}'. Using default voice.");
                voice = GetPersonaDef(storytellerDefName)?.voiceProviders.FirstOrDefault(vm => vm.name == "default")?.voice;
            }

            return voice;
        }

        public static string GetEmotionalTone(string storytellerDefName, string emotion)
        {
            return GetPersonaDef(storytellerDefName)?.GetEmotion(emotion) ?? "";
        }

        internal static string GetMood(string storytellerDefName, string mood)
        {
            return GetPersonaDef(storytellerDefName)?.GetMood(mood) ?? "";
        }

        internal static object GetGender(string storytellerDefName)
        {
            return GetPersonaDef(storytellerDefName)?.gender ?? "other";
        }
    }
}