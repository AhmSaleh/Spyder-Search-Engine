using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections;
using System.Data.SqlClient;
using StopWord;

namespace SpyderWeb.Models
{
    public class Searcher
    {
        // The SQL Database connection string || Change it according to your PC
        private string sqlDatabase = "Data Source=DESKTOP-PO5RE8I;Initial Catalog=webcrawler_database;Integrated Security=True";

        // List of documents IDs and their scores
        List<KeyValuePair<int, int>> documentScores;

        // List of documents with missing terms, and the num of terms
        List<KeyValuePair<int, int>> documentNumOfTerms;

        public Searcher() { }


        public List<string> SearchMultiKeywords(string query)
        {
            // Emptying the lists of scores and num of terms
            documentScores = new List<KeyValuePair<int, int>>();
            documentNumOfTerms = new List<KeyValuePair<int, int>>();

            // Get different terms
            string[] terms = query.Split(" ");

            // List of each document's terms and their positions
            List<KeyValuePair<string, int>> data = new List<KeyValuePair<string, int>>();

            // Get documents containing the searched terms
            SqlConnection conn = new SqlConnection(sqlDatabase);
            conn.Open();

            // Selecting terms that are included in the terms the user is querying
            string cmdText = "SELECT term, position, docID FROM indexer_positions WHERE term IN("
                            + string.Join(",", terms.Select(t => "'" + t + "'"))
                            + ") ORDER BY docID;";

            SqlCommand cmd = new SqlCommand(cmdText, conn);

            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                // Some helpful variables
                string term;
                int position;
                int currentDocID = 1;
                bool firstRead = true;
                Hashtable distinctValues;

                while (reader.Read())
                {
                    /* If it's the first time reading the rows, use the first row's
                    docID as the current one */
                    if (firstRead)
                    {
                        currentDocID = int.Parse(reader["docID"].ToString());
                        firstRead = false;
                    }

                    // Storing the current term and its position
                    term = reader["term"].ToString();
                    position = int.Parse(reader["position"].ToString());

                    /* If the reader is reading from the same document, add all its
                     terms with their positions in the data KeyValue list */
                    if (currentDocID == int.Parse(reader["docID"].ToString()))
                        data.Add(new KeyValuePair<string, int>(term, position));

                    /* Otherwise, calculate the document's scores and add it the documentsScores list */
                    else
                    {
                        /* This hashtable is used to count the distinct values (terms)
                         inside the data KeyValue of each document */
                        distinctValues = new Hashtable();
                        for (int i = 0; i < data.Count; i++)
                        {
                            if (!distinctValues.ContainsKey(data[i].Key))
                            {
                                distinctValues.Add(data[i].Key, true);
                            }                          
                        }

                        /* If the number of distinct values is equal to the 
                         number of queried terms, we calculate the score for the document*/
                        if (distinctValues.Count == terms.Length)
                        {
                            documentScores.Add(new KeyValuePair<int, int>(currentDocID, GetProximtyScore(data)));
                        }
                        /* Otherwise, we calculate its priority based on the number of terms contaitned
                         out of the queried terms*/
                        else
                        {
                            documentNumOfTerms.Add(new KeyValuePair<int, int>(currentDocID, distinctValues.Count));
                        }

                        // Clearing the data list
                        data = new List<KeyValuePair<string, int>>();
                    }

                    // Moving the currentDocID to the next
                    currentDocID = int.Parse(reader["docID"].ToString());
                } 
                    
            }
            conn.Close();

            // Sorting docs according to their scores
            documentScores.Sort((b, a) => (b.Value.CompareTo(a.Value)));

            // Sorting docs according to their num. of terms
            documentNumOfTerms.Sort((a, b) => (b.Value.CompareTo(a.Value)));

            // Returning the list of links
            return GetDocuments();
        }

        public List<string> SearchExactPhrase(string query)
        {
            // Emptying the lists of scores and num of terms
            documentScores = new List<KeyValuePair<int, int>>();
            documentNumOfTerms = new List<KeyValuePair<int, int>>();

            // Get different terms
            string[] terms = query.Split(" ");
            string[] noStopwordsQuery = query.RemoveStopWords("en").Split(" ");
            int[] termIndexes = new int[noStopwordsQuery.Length];

            for (int i = 0; i < noStopwordsQuery.Length; i++)
            {
                for (int j = 0; j < terms.Length; j++)
                {
                    if (noStopwordsQuery[i].Equals(terms[j]))
                    {
                        termIndexes[i] = j;
                    }
                }
            }

            // List of each document's terms and their positions
            List<KeyValuePair<string, int>> data = new List<KeyValuePair<string, int>>();

            // Get documents containing the searched terms
            SqlConnection conn = new SqlConnection(sqlDatabase);
            conn.Open();

            // Selecting terms that are included in the terms the user is querying
            string cmdText = "SELECT term, position, docID FROM indexer_positions WHERE term IN("
                            + string.Join(",", terms.Select(t => "'" + t + "'"))
                            + ") ORDER BY docID;";

            SqlCommand cmd = new SqlCommand(cmdText, conn);

            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                // Some helpful variables
                string term;
                int position;
                int currentDocID = 1;
                bool firstRead = true;

                while (reader.Read())
                {
                    /* If it's the first time reading the rows, use the first row's
                    docID as the current one */
                    if (firstRead)
                    {
                        currentDocID = int.Parse(reader["docID"].ToString());
                        firstRead = false;
                    }

                    // Storing the current term and its position
                    term = reader["term"].ToString();
                    position = int.Parse(reader["position"].ToString());

                    /* If the reader is reading from the same document, add all its
                     terms with their positions in the data KeyValue list */
                    if (currentDocID == int.Parse(reader["docID"].ToString()))
                        data.Add(new KeyValuePair<string, int>(term, position));

                    else
                    {
                        // Sorting terms according to their indexes
                        data.Sort((b, a) => (b.Value.CompareTo(a.Value)));

                        int counter = 0;
                        int queryIndex = 0;
                        for (int i = 0; i < data.Count - 1; i++)
                        {
                            if(noStopwordsQuery[queryIndex].Equals(data[i].Key)
                                && noStopwordsQuery[queryIndex+1].Equals(data[i+1].Key))
                            {
                                if (termIndexes[queryIndex + 1] - termIndexes[queryIndex] == data[i + 1].Value - data[i].Value)
                                    queryIndex++;

                                else
                                    queryIndex = 0;
                            }

                            else
                                queryIndex = 0;

                            if (queryIndex == noStopwordsQuery.Length - 1)
                            {
                                counter++;
                                queryIndex = 0;
                            }
                        }

                        if (counter >= 1)
                        {
                            documentScores.Add(new KeyValuePair<int, int>(currentDocID, counter));
                        }

                        // Clearing the data list
                        data = new List<KeyValuePair<string, int>>();
                    }

                    currentDocID = int.Parse(reader["docID"].ToString());
                }
                conn.Close();

                // Sorting docs according to their scores
                documentScores.Sort((a, b) => (b.Value.CompareTo(a.Value)));

                // Returning the list of links
                return GetDocuments();
            }
        }

        public List<string> GetDocuments()
        {
            // List of links
            List<string> links = new List<string>();

            // Get document links using their IDs
            SqlConnection conn = new SqlConnection(sqlDatabase);
            conn.Open();

            // Selecting the links according to their docIDs
            string cmdText = "SELECT urlLink FROM t WHERE docId=@docId;";
            SqlCommand cmd;
            SqlDataReader reader;
            int docId;

            // For each document in the documentScores list, select its link and add it to the links list
            for (int i = 0; i < documentScores.Count; i++)
            {
                docId = documentScores[i].Key;
                cmd = new SqlCommand(cmdText, conn);
                cmd.Parameters.AddWithValue("@docId", docId);

                reader = cmd.ExecuteReader();
                reader.Read();

                links.Add(reader["urlLink"].ToString());
                reader.Close();
            }

            // For each document in the documentNumOfTerms list, select its link and add it to the links list
            for (int i = 0; i < documentNumOfTerms.Count; i++)
            {
                docId = documentNumOfTerms[i].Key;
                cmd = new SqlCommand(cmdText, conn);
                cmd.Parameters.AddWithValue("@docId", docId);

                reader = cmd.ExecuteReader();
                reader.Read();

                links.Add(reader["urlLink"].ToString());
                reader.Close();
            }
            conn.Close();

            // Returning the list of links
            return links;
        }

        public static int GetProximtyScore(List<KeyValuePair<string, int>> data)
        {

            Hashtable ht;
            int distance;
            int lastIndex;
            int min = 2147483647;

            data.Sort((b, a) => (b.Value.CompareTo(a.Value)));

            for (int i = 0; i < data.Count; i++)
            {

                ht = new Hashtable();
                distance = 0;
                ht.Add(data[i].Key, true);
                lastIndex = data[i].Value;

                for (int j = i + 1; j < data.Count; j++)
                {
                    if (!ht.ContainsKey(data[j].Key))
                    {
                        ht.Add(data[j].Key, true);
                        distance += (data[j].Value - lastIndex);
                        lastIndex = data[j].Value;
                    }
                }

                min = Math.Min(min, distance);
            }

            return min;
        }
    }
}
