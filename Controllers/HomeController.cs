using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SpyderWeb.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace SpyderWeb.Controllers
{
    public class HomeController : Controller
    {
        private Spellchecker spellchecker = new Spellchecker();
        private Searcher searcher = new Searcher();

        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        // Homepage
        public IActionResult Index()
        {
            return View();
        }

        // Search page
        [HttpPost]
        [Route("/Search")]
        public IActionResult Search(string query)
        {
            if (query[0] == '\"' && query[query.Length - 1] == '\"'
                && query.Split(" ").Length > 1)
            {
                query = new Indexer().RemovePun(query).ToLower();
                ViewData["documents"] = searcher.SearchExactPhrase(query);
            }

            else
            {
                query = new Indexer().RemovePun(query).ToLower();
                ViewData["documents"] = searcher.SearchMultiKeywords(query);
            }

            ViewData["correction"] = spellchecker.CalculateQueryKGrams(query);
            ViewData["query"] = query;
            return View();
        }

        [Route("/kgrams")]
        public IActionResult KGrams()
        {
            //spellchecker.CalculateAllKGrams();
            return new EmptyResult();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
