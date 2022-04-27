using mshtml;
using NTextCat;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using NUglify;

namespace SpyderWeb.Models
{
    public class Crawler
    {
        public HashSet<string> uniqueVisitedLinks;
        public Queue<string> visited;
        string url;

        static string sqlDatabase;
        //static SqlConnection conn;

        public Crawler(string url)
        {
            uniqueVisitedLinks = new HashSet<string>();
            visited = new Queue<string>();
            sqlDatabase = "Data Source=DESKTOP-PO5RE8I;Initial Catalog=webcrawler_database;Integrated Security=True";
            this.url = url;

        }

        // The main crawling function
        public void Crawling()
        {
            // If the URL was not visited before, added to the seed list
            if (!uniqueVisitedLinks.Contains(url))
            {
                uniqueVisitedLinks.Add(url);
                visited.Enqueue(url);
            }

            int count = 1;
            while (count < 1000)
            {
                try
                {
                    url = visited.Dequeue();
                    string rString = Fetch(url);
                    IHTMLDocument2 doc = new HTMLDocumentClass();
                    doc.write(rString);
                    IHTMLElementCollection elements = doc.links;

                    foreach (IHTMLElement el in elements)
                    {
                        string link = (string)el.getAttribute("href", 0);

                        if (!(link.Contains("https") || link.Contains("http")) || (uniqueVisitedLinks.Contains(link)) || !CheckLang(link))
                            continue;

                        rString = Fetch(link);
                        Console.WriteLine(link);
                        uniqueVisitedLinks.Add(link);
                        visited.Enqueue(link);

                        //save in database 
                        SqlConnection conn = new SqlConnection(sqlDatabase);
                        conn.Open();

                        string cmdText = "INSERT INTO t(urlLink,body) VALUES (@no,@name)";
                        SqlCommand cmd = new SqlCommand(cmdText, conn);
                        cmd.Parameters.AddWithValue("@no", link);
                        cmd.Parameters.AddWithValue("@name", rString);

                        cmd.ExecuteNonQuery();
                        conn.Close();

                        count++;
                    }

                }
                catch { }

            }
        }

        // Checking the page's language
        public bool CheckLang(string link)
        {
            string rString = Fetch(link);
            IHTMLDocument2 myDoc = new HTMLDocumentClass();
            myDoc.write(rString);

            if (myDoc.body == null) return false;

            var factory = new RankedLanguageIdentifierFactory();
            var identifier =
                factory.Load("E:\\College Material\\4th Year\\2nd Semester\\3) Information Retrieval\\projectIR\\projectIR\\Core14.profile.xml"); // can be an absolute or relative path. Beware of 260 chars limitation of the path length in Windows. Linux allows 4096 chars.
            var languages = identifier.Identify(myDoc.body.innerText);

            var mostCertainLanguage = languages.FirstOrDefault();

            if (mostCertainLanguage == null) return false;

            if (mostCertainLanguage.Item1.Iso639_3 == "eng" || myDoc.body.lang == "eng")
                return true;

            return false;
        }


        // Fetching a URL
        public string Fetch(string link)
        {
            WebRequest request = WebRequest.Create(link);
            WebResponse response = request.GetResponse();

            Stream streamResponse = response.GetResponseStream();
            StreamReader streamReader = new StreamReader(streamResponse);

            string rString = streamReader.ReadToEnd();
            streamResponse.Close();
            streamReader.Close();
            response.Close();

            return rString;

        }

        // Checking if a URL exists
        public bool CheckRemoteUrlExists(string url)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.AllowAutoRedirect = false;
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                response.Close();
                return (response.StatusCode == HttpStatusCode.OK);
            }
            catch
            {
                return false;
            }

        }

        public string ParseHtmlText()
        {
            SqlConnection conn = new SqlConnection(sqlDatabase);
            string cmdText = "SELECT * body FROM t";
            SqlCommand cmd = new SqlCommand(cmdText, conn);
            string body = "";

            conn.Open();
            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    body = reader["body"].ToString();
                }
            }
            conn.Close();

            return NUglify.Uglify.HtmlToText(body).Code;
        }
    }
}
