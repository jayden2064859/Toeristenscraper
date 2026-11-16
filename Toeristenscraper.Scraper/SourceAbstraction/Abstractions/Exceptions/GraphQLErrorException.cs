using System;
using System.Collections.Generic;
using System.Text;

namespace Toeristenscraper.Scraper.SourceAbstraction.Abstractions.Exceptions
{
    // wordt teruggegeven wanneer GraphQL een 200 response teruggeeft, waarbij de inhoud alsnog onbruikbaar is
    public class GraphQLErrorException(string message) : Exception(message) { }
}
