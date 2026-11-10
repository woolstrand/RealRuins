using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Verse;

namespace RealRuins.WhatsNew
{
    public class NewsArticle
    {
        public string id;    // file name without extension, the same in every language
        public string title;
        public string text;
        public bool important; // <important>true</important>: closing it unread asks for confirmation
    }

    /// <summary>
    /// What's new articles: one XML file per article in News/&lt;language folder&gt;/ of the mod, e.g. News/English/2026-11.xml:
    /// &lt;NewsArticle&gt;&lt;title&gt;...&lt;/title&gt;&lt;text&gt;...&lt;/text&gt;&lt;important&gt;true&lt;/important&gt;&lt;/NewsArticle&gt;
    /// (important is optional, see Dialog_WhatsNew). A file missing in the active
    /// language is taken from English. File names sort by date, newest last.
    /// Read ids are kept in a file of their own in the config folder, not in the mod settings: resetting the settings
    /// must not bring old news back.
    /// </summary>
    public static class NewsArticles
    {
        private const string NewsFolder = "News";
        private const string DefaultLanguageFolder = "English";
        private const string ReadFileName = "RealRuins_ReadNews.txt";

        private static HashSet<string> readIds;

        private static string ReadFilePath => Path.Combine(GenFilePaths.ConfigFolderPath, ReadFileName);

        /// <summary>Articles not read yet, newest first.</summary>
        public static List<NewsArticle> Unread()
        {
            HashSet<string> read = ReadIds();
            return All().Where(a => !read.Contains(a.id)).ToList();
        }

        /// <summary>Every article, newest first.</summary>
        public static List<NewsArticle> All()
        {
            List<NewsArticle> articles = new List<NewsArticle>();
            string root = NewsRoot();
            if (root == null) return articles;

            string fallbackFolder = Path.Combine(root, DefaultLanguageFolder);
            string languageFolder = Path.Combine(root, LanguageDatabase.activeLanguage?.folderName ?? DefaultLanguageFolder);
            if (!Directory.Exists(fallbackFolder)) return articles;

            foreach (string fallbackFile in Directory.GetFiles(fallbackFolder, "*.xml").OrderByDescending(f => Path.GetFileName(f), StringComparer.Ordinal))
            {
                string localized = Path.Combine(languageFolder, Path.GetFileName(fallbackFile));
                NewsArticle article = (File.Exists(localized) ? Load(localized) : null) ?? Load(fallbackFile);
                if (article != null) articles.Add(article);
            }
            return articles;
        }

        public static bool IsRead(NewsArticle article) => ReadIds().Contains(article.id);

        public static void MarkRead(IEnumerable<NewsArticle> articles)
        {
            HashSet<string> read = ReadIds();
            bool changed = false;
            foreach (NewsArticle article in articles)
            {
                changed |= read.Add(article.id);
            }
            if (!changed) return;
            try
            {
                File.WriteAllLines(ReadFilePath, read.OrderBy(id => id, StringComparer.Ordinal).ToArray());
            }
            catch (Exception e)
            {
                Log.Warning("[RealRuins] Could not save read news to " + ReadFilePath + ": " + e.Message);
            }
        }

        /// <summary>Forgets every read article (debug).</summary>
        public static void ForgetRead()
        {
            readIds = new HashSet<string>();
            try
            {
                if (File.Exists(ReadFilePath)) File.Delete(ReadFilePath);
            }
            catch (Exception e)
            {
                Log.Warning("[RealRuins] Could not delete " + ReadFilePath + ": " + e.Message);
            }
        }

        private static HashSet<string> ReadIds()
        {
            if (readIds != null) return readIds;
            readIds = new HashSet<string>();
            try
            {
                if (File.Exists(ReadFilePath))
                {
                    foreach (string line in File.ReadAllLines(ReadFilePath))
                    {
                        if (!line.Trim().NullOrEmpty()) readIds.Add(line.Trim());
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warning("[RealRuins] Could not read " + ReadFilePath + ": " + e.Message);
            }
            return readIds;
        }

        private static string NewsRoot()
        {
            ModContentPack content = LoadedModManager.GetMod<RealRuins_Mod>()?.Content;
            if (content == null) return null;
            string root = Path.Combine(content.RootDir, NewsFolder);
            return Directory.Exists(root) ? root : null;
        }

        private static NewsArticle Load(string path)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(path);
                XmlNode node = doc.DocumentElement;
                string title = node?.SelectSingleNode("title")?.InnerText?.Trim();
                string text = node?.SelectSingleNode("text")?.InnerText?.Trim();
                if (title.NullOrEmpty() && text.NullOrEmpty()) return null;
                return new NewsArticle {
                    id = Path.GetFileNameWithoutExtension(path),
                    title = title ?? "",
                    text = text ?? "",
                    important = string.Equals(node?.SelectSingleNode("important")?.InnerText?.Trim(), "true", StringComparison.OrdinalIgnoreCase)
                };
            }
            catch (Exception e)
            {
                Log.Warning("[RealRuins] Could not load news article " + path + ": " + e.Message);
                return null;
            }
        }
    }
}
