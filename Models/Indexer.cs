using System;
using System.Collections.Generic;
using System.Text;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using NUglify;
using System.Collections;
using Porter2StemmerStandard;
using StopWord;

namespace SpyderWeb.Models
{
    public class Indexer
    {
        // The SQL Database connection string || Change it according to your PC
        private string sqlDatabase = "Data Source=DESKTOP-PO5RE8I;Initial Catalog=webcrawler_database;Integrated Security=True";

        /*Key: the term itself
          Value: A list, where the first index is the frequency of the term
          and from the second index onwards is a list of all the term's positions */
        Dictionary<string, List<int>> termInfo;

        public Indexer()
        { }

        public void StartIndexing()
        {
            // Retrieving first 1500 documents from the DB
            SqlConnection conn = new SqlConnection(sqlDatabase);
            string cmdText = "SELECT TOP 1800 body, docID FROM t";
            SqlCommand cmd = new SqlCommand(cmdText, conn);

            int count = 0;

            conn.Open();
            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read() && count < 1500)
                {
                    // Initializing the terms dictionary and the docID
                    termInfo = new Dictionary<string, List<int>>();
                    int docId = int.Parse(reader["docID"].ToString());

                    // Parsing the HTML body
                    string body = reader["body"].ToString();
                    string parsedText = "";
                    try
                    {
                        parsedText = Uglify.HtmlToText(body).Code;
                    }
                    catch { }

                    if (parsedText == "") continue;

                    // Tokenizing the HTML body
                    string[] tokenizedText = Tokenize(parsedText);

                    // Applying Linguistic Algorithms to the tokenized array of words
                    for (int i = 0; i < tokenizedText.Length; i++)
                    {
                        string term = DoLinguistics(tokenizedText[i], docId);

                        // Making sure the terms isn't empty
                        if (string.IsNullOrWhiteSpace(term))
                            continue;

                        // If the term is already inside the dictionary, increment its frequency and add a new position
                        if (termInfo.ContainsKey(term))
                        {
                            termInfo[term][0]++;
                            termInfo[term].Add(i);
                        }

                        // Else, add a new term with a frequency of 1 and its position
                        else
                        {
                            termInfo[term] = new List<int> { 1, i };
                        }
                    }

                    // Initialising insertion commands
                    SqlConnection conn2 = new SqlConnection(sqlDatabase);
                    string firstCmdText = "INSERT INTO indexer(term, docID, frequency) VALUES (@term, @docID, @freq)";
                    string secondCmdText = "INSERT INTO indexer_positions(term, docID, position) VALUES (@term, @docID, @pos)";

                    // Each term needs to be add to the the indexer and indexer_positions tables
                    conn2.Open();
                    for (int i = 0; i < termInfo.Count; i++)
                    {
                        string term = termInfo.Keys.ElementAt(i);

                        // The first command to add the term to the indexer table
                        SqlCommand firstCmd = new SqlCommand(firstCmdText, conn2);
                        firstCmd.Parameters.AddWithValue("@term", term);
                        firstCmd.Parameters.AddWithValue("@docID", docId);
                        firstCmd.Parameters.AddWithValue("@freq", termInfo[term][0]);
                        try
                        {
                            firstCmd.ExecuteNonQuery();
                        }
                        catch { }

                        // The second command to add each term with each position to the indexer_positions table
                        for (int j = 1; j < termInfo[term].Count; j++)
                        {
                            SqlCommand secondCmd = new SqlCommand(secondCmdText, conn2);
                            secondCmd.Parameters.AddWithValue("@term", term);
                            secondCmd.Parameters.AddWithValue("@docID", docId);
                            secondCmd.Parameters.AddWithValue("@pos", termInfo[term][j]);
                            try
                            {
                                secondCmd.ExecuteNonQuery();
                            }
                            catch { }
                        }
                    }
                    conn2.Close();
                    Console.WriteLine("Document #" + docId + " successfully indexed!");
                    count++;
                }
            }
            conn.Close();
            Console.WriteLine("===================================");
            Console.WriteLine("All documents successfully indexed! Tam ta3be2at al database b naga7 :D");
        }
        private void SaveBeforeStemming(string term, int docID)
        {
            // Tnsert term with docID into the DB
            SqlConnection conn = new SqlConnection(sqlDatabase);
            string cmdText = "INSERT INTO all_terms(term, docID) VALUES (@term, @docID)";

            SqlCommand cmd = new SqlCommand(cmdText, conn);
            cmd.Parameters.AddWithValue("@term", term);
            cmd.Parameters.AddWithValue("@docID", docID);

            conn.Open();
            try
            {
                cmd.ExecuteNonQuery();
            }
            catch { }

            finally
            {
                conn.Close();
            }

        }
        private string[] Tokenize(string doc)
        {
            return doc.Split(new char[] { ' ', ',', '\n' });
        }
        private string DoLinguistics(string word, int docId)
        {
            // Applying linguistics
            word = RemovePun(word).ToLower();

            if (string.IsNullOrWhiteSpace(word))
                return " ";

            // Saving (for the spell check module)
            SaveBeforeStemming(word, docId);

            // Stemming using Porter2Stemmer, and removing StopWords
            try
            {
                var stemmer = new EnglishPorter2Stemmer();
                word = stemmer.Stem(word).Value;
            }
            catch { }

            return word.RemoveStopWords("en");
        }
        public string RemovePun(string word)
        {
            string newWord = "";
            // A Hashtable of several special characters to be removed
            Hashtable ht = new Hashtable();
            ht.Add('.', true);
            ht.Add('*', true);
            ht.Add(',', true);
            ht.Add('_', true);
            ht.Add('-', true);
            ht.Add('?', true);
            ht.Add('!', true);
            ht.Add('"', true);
            ht.Add(':', true);
            ht.Add(';', true);
            ht.Add('(', true);
            ht.Add(')', true);
            ht.Add('{', true);
            ht.Add('}', true);
            ht.Add('[', true);
            ht.Add(']', true);
            ht.Add('`', true);
            ht.Add('/', true);
            ht.Add('&', true);
            ht.Add('\\', true);
            ht.Add('\'', true);
            ht.Add('„', true);
            ht.Add('@', true);
            ht.Add('©', true);
            ht.Add('•', true);
            ht.Add('–', true);
            ht.Add('·', true);
            ht.Add('|', true);
            ht.Add('#', true);
            ht.Add('>', true);
            ht.Add('<', true);

            // For each character in the word, remove it if it's in the Hashtable
            for (int i = 0; i < word.Length; i++)
            {
                if (!ht.ContainsKey(word[i]))
                {
                    newWord += word[i];
                }
            }

            return newWord;
        }
    }
}
