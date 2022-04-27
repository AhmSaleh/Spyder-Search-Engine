using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace SpyderWeb.Models
{
    public class Spellchecker
    {
        // The SQL Database connection string || Change it according to your PC
        private string sqlDatabase = "Data Source=DESKTOP-PO5RE8I;Initial Catalog=webcrawler_database;Integrated Security=True";

        // A dictionary carrying all K-grams
        Dictionary<string, HashSet<string>> dict = new Dictionary<string, HashSet<string>>();

        // English dictionary
        Hashtable englishDictionary = new Hashtable();

        public Spellchecker()
        {
            LoadDictionary();
        }

        // Used for calculating the K-Grams for all previously-saved terms 
        public void CalculateAllKGrams()
        {
            // Retrieving all terms from the DB
            SqlConnection conn = new SqlConnection(sqlDatabase);
            string cmdText = "SELECT term FROM all_terms";
            SqlCommand cmd = new SqlCommand(cmdText, conn);

            conn.Open();
            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                string term;

                while (reader.Read())
                {
                    term = reader["term"].ToString();

                    if (term.Length > 1)
                        CalculateTermKGrams(term);
                }
            }
            conn.Close();

            cmdText = "INSERT INTO k_grams (gram, term) VALUES (@gram, @term)";
            
            conn.Open();
            foreach (var entry in dict)
            {
                string gram = entry.Key;
                foreach (var term in entry.Value)
                {
                    cmd = new SqlCommand(cmdText, conn);
                    cmd.Parameters.AddWithValue("@gram", gram);
                    cmd.Parameters.AddWithValue("@term", term);

                    try
                    {
                        cmd.ExecuteNonQuery();
                    }
                    catch {
                        System.Diagnostics.Debug.WriteLine("Term: " + term + " Gram: " + gram + " already added!");
                    }
                }
            }
            conn.Close();
        }


        // An auxiliary function that helps calculate the K-Grams for all terms
        public void CalculateTermKGrams(string term)
        {
            List<string> list = new List<string>();

            string editedWord = '$' + term + '$';

            for (int i = 0; i <= editedWord.Length - 3; i++)
                list.Add(editedWord.Substring(i, 3));


            for (int i = 0; i < list.Count; i++)
            {
                if (!dict.ContainsKey(list[i]))
                    dict.Add(list[i], new HashSet<string>());

                dict[list[i]].Add(term);
            }

            /*foreach (var entry in dict)
            {

                Console.WriteLine("------------");
                Console.WriteLine("||||" + entry.Key + "||||");
                Console.WriteLine("------------");

                foreach (var j in entry.Value)
                    Console.WriteLine(j);

                Console.WriteLine("------------");
            }*/
        }

        
        // input: string query
        // output: List of strings ?
        public string CalculateQueryKGrams(string query)
        {
            string[] terms = query.Split(" ");

            string possibleCorrection = "";
            Dictionary<string, int> termGrams;
            List<string> grams;

            for (int k = 0; k < terms.Length; k++)
            {
                bool isCorrect = false;
                termGrams = new Dictionary<string, int>();
                grams = new List<string>();

                string word = terms[k];
                string editedWord = '$' + word + '$';

                for (int i = 0; i <= editedWord.Length - 3; i++)
                    grams.Add(editedWord.Substring(i, 3));

                // Retrieving all terms and their grams from the DB
                SqlConnection conn = new SqlConnection(sqlDatabase);
                string cmdText = "SELECT term FROM k_grams WHERE gram IN("
                    + string.Join(",", grams.Select(g => "'" + g + "'"))
                    + ");";
                SqlCommand cmd = new SqlCommand(cmdText, conn);

                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    string term;
                    while (reader.Read())
                    {
                        term = reader["term"].ToString();

                        if (word.Equals(term))
                        {
                            isCorrect = true;
                            break;
                        }

                        if (!CheckEnglishWord(term))
                            continue;

                        if (!termGrams.ContainsKey(term))
                            termGrams.Add(term, 1);

                        else
                            termGrams[term]++;
                    }
                }
                conn.Close();

                if (!isCorrect)
                {
                    var sorted = termGrams.OrderByDescending(x => x.Value).ThenBy(x => x.Key);
                    possibleCorrection += sorted.First().Key + " ";
                }

                else
                {
                    possibleCorrection += word + " ";
                }
            }

            return possibleCorrection.Substring(0, possibleCorrection.Length - 1);
        }

        public void LoadDictionary()
        {
            string[] lines = System.IO.File.ReadAllLines("englishDictionary.txt");
            foreach (string line in lines)
            {
                foreach (string word in line.Split(" "))
                {
                    if (!englishDictionary.ContainsKey(word))
                        englishDictionary.Add(word, 1);
                }
            }
        }

        public bool CheckEnglishWord(string term)
        {
            if (englishDictionary.ContainsKey(term))
                return true;

            return false;
        }
    }
}
