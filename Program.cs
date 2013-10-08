using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;

namespace Softable
{
    class Program
    {
        public delegate bool PatternLookupMethod(string data, string pattern);
        public delegate string DataPreparationMethod(string Input);
        public delegate string PatternPreparationMethod(string Input);
        
        static void Main(string[] args)
        {
            string DefaultOutputFileName = "output.txt";
            string DefaultListingsFileName = "listings.txt";
            string DefaultProductsFileName = "products.txt";
            string OutputFileName= String.Empty;
            string ListingsFileName = String.Empty;
            string ProductsFileName = string.Empty;
            if (args.Length != 3)
            {
                OutputFileName = DefaultOutputFileName;
                ListingsFileName = DefaultListingsFileName;
                ProductsFileName = DefaultProductsFileName;
            }
            else
            {
                OutputFileName = args[2];
                ListingsFileName = args[0];
                ProductsFileName = args[1];
            }
            ListOfProducts Products = new ListOfProducts(ProductsFileName);
            ListOfListings Listings = new ListOfListings(ListingsFileName);

            Listings.PatternPreparationMethods.Add(new PatternPreparationMethod(ConvertPattern));
            Listings.LookupMethods.Add(new ProcessDataMethod(DirectScanRegex));

            string Output = Listings.Match(Products);
            File.WriteAllText(OutputFileName, Output);
            Exit(String.Concat("Done! Output file name: \"", OutputFileName, "\". Press any key..."));
        }
        private static bool FindPattern(string where, string what, float threshold)
        {
            bool PatternFound = true;
            bool PatternNotFoud = false;
            int ScanLength;
            int MatchCount;
            float CurrentThreshold;

            #region Remove Spaces and Underscores
            StringBuilder Sb1 = new StringBuilder(where.Length);
            StringBuilder Sb2 = new StringBuilder(what.Length);
            List<Char> CharsToRemove = new List<Char>();
            CharsToRemove.Add(' ');
            CharsToRemove.Add('_');
            CharsToRemove.Add('-');
            CharsToRemove.Add('.');
            CharsToRemove.Add('/');
            for(int CharIndex = 0; CharIndex < where.Length; CharIndex++)
            {
                if(CharsToRemove.IndexOf(where[CharIndex]) == -1)
                {
                    Sb1.Append(where[CharIndex]);
                }
            }
            string Data = Sb1.ToString().ToLower();
            for (int CharIndex = 0; CharIndex < what.Length; CharIndex++)
            {
                if (CharsToRemove.IndexOf(what[CharIndex]) == -1)
                {
                    Sb2.Append(what[CharIndex]);
                }
            }
            string Pattern = Sb2.ToString().ToLower();
            #endregion
            #region DIRECT SCAN
            /*
             * scan from beginning to end- length of what
             * DIRECT SCAN:
             * if where.symbol == what.symbol then compare next, count matches
             * if next is a match, keep counting unless found first notmatch.
             * if matchcount/what.length > threshold then return pattern found
             */
            ScanLength = Data.Length;
            MatchCount = 0;
            CurrentThreshold = 0;

            for (int CharIndex = 0; CharIndex < ScanLength; CharIndex++)
            {
                if (Data[CharIndex] == Pattern[MatchCount])
                {
                    MatchCount++;
                }
                else
                {
                    MatchCount = 0;
                }
                CurrentThreshold = (float)MatchCount / (float)Pattern.Length;
                if (CurrentThreshold >= threshold)
                {
                    return PatternFound;
                }
            }
            #endregion
            #region FORWARD COUNTDOWN
            /* FORWARD COUNTDOWN
             * keep scanning taking one char out from the beginning of while till what.Newlength/what.OriginalLength > Threshold && MatchNotFound
             * If MatchFound => return pattern found;
             */
            ScanLength = Data.Length;
            MatchCount = 0;
            CurrentThreshold = 0;
            int CurrentCountDown = 1;
            while (((float)CurrentCountDown / (float)Pattern.Length) <= (1.0f - threshold))
            {
                for (int CharIndex = 0; CharIndex < ScanLength; CharIndex++)
                {
                    if (Data[CharIndex] == Pattern[CurrentCountDown + MatchCount])
                    {
                        MatchCount++;
                    }
                    else
                    {
                        MatchCount = 0;
                    }
                    CurrentThreshold = (float)MatchCount / (float)Pattern.Length;
                    if (CurrentThreshold >= threshold)
                    {
                        return PatternFound;
                    }
                }
                CurrentCountDown++;
            }
            #endregion
            #region MISMATCHES WITHIN DATA
            /* MISMATCHES WITHIN DATA
             * if first what matched where and next didn't, continue scanning where for chars (1-threshold)*what.Length;
             * if match found; keep scaning both untill either match found or mismatch chars count > (1-threshold)*what.Length;
             */
            ScanLength = Data.Length;
            MatchCount = 0;
            CurrentThreshold = 0;
            int PatternStartIndex = 0;
            bool PatternSuspected = false;
            int MismatchCount = 0;
            for (int CharIndex = 0; CharIndex < ScanLength; CharIndex++)
            {
                if (Data[CharIndex] == Pattern[MatchCount])
                {
                    // First char matched: suspect pattern
                    PatternSuspected = true;
                    MatchCount++;
                }
                else if (PatternSuspected && Data[CharIndex] == Pattern[PatternStartIndex])
                {
                    //see if current pattern will continue...
                    if(CharIndex == (Data.Length-1)) break;
                    if (Data[CharIndex + 1] == Pattern[MatchCount])
                    {
                        CharIndex++;
                        MatchCount++;
                        continue;
                    }
                    // New Pattern suspected, start
                    MatchCount = 1;
                }
                else if (PatternSuspected && Data[CharIndex] != Pattern[MatchCount])
                {
                    MismatchCount++;
                    if (((float)MismatchCount / Pattern.Length) > (1.0f - threshold))
                    {
                        PatternSuspected = false;
                        MatchCount = 0;
                    }
                }

                // if pattern is suspected, check if pattern found:
                if (PatternSuspected)
                {
                    CurrentThreshold = (float)MatchCount / (float)Pattern.Length;
                    if (CurrentThreshold >= threshold)
                    {
                        return PatternFound;
                    }
                }
            }


            #endregion

            return PatternNotFoud; 
        }
        public class ListOfListings
        {
            private List<Listing> ListOfListings_ = new List<Listing>();
            private List<ProcessDataMethod> LookupMethods_ = new List<ProcessDataMethod>();
            private List<DataPreparationMethod> DataPreparationMethods_ = new List<DataPreparationMethod>();
            private List<PatternPreparationMethod> PatternPreparationMethods_ = new List<PatternPreparationMethod>();
            public ListOfListings(string FileName)
            {
                if (!File.Exists(FileName))
                {
                    Exit(String.Concat("Listing file does not exist! (",FileName,")"));
                }
                string[] DataFromFile = File.ReadAllLines(FileName);
                foreach (string DataLine in DataFromFile)
                {
                    ListOfListings_.Add(new Listing(DataLine));
                }
            }

            public string Match(ListOfProducts Products)
            {
                StringBuilder Output = new StringBuilder();
                List<Listing> MatchedListings;
                Output.Append("[");
                foreach (Product CurrentProduct in Products.ProductList)
                {
                    if (CurrentProduct == null) continue;
//                    MatchedListings = ListOfListings_.Where(l => FindPattern(where: l.Manufacturer, what: CurrentProduct.Manufacturer) && FindPattern(where: l.Title, what: CurrentProduct.Family) && FindPattern(where: l.Title, what: CurrentProduct.Model)).ToList();
                    MatchedListings = ListOfListings_.Where(l => FindPattern(where: l.Manufacturer, what: CurrentProduct.Manufacturer) && FindPattern(where: l.Title, what: CurrentProduct.Model)).ToList();
                    if (MatchedListings.Count > 0)
                    {
                        Output.Append(CreateObjects(CurrentProduct, MatchedListings));
                    }
                }
                if (Output.Length > 0) Output.Length--;
                Output.Append("]");
                return Output.ToString();
            }
            public List<ProcessDataMethod> LookupMethods
            {
                get { return LookupMethods_; }
            }
            public List<DataPreparationMethod> DataPreparationMethods
            {
                get { return DataPreparationMethods_; }
            }
            public List<PatternPreparationMethod> PatternPreparationMethods
            {
                get { return PatternPreparationMethods_; }
            }

            private bool FindPattern(string where, string what)
            {
                bool PatternFound = true;
                bool PatternNotFound = false;

                if (where == null || what == null) return PatternNotFound;

                string Data = where;
                foreach (DataPreparationMethod PrepareData in DataPreparationMethods_)
                {
                    Data = PrepareData(Data);
                }
                string Pattern = what;
                foreach (PatternPreparationMethod PreparePattern in PatternPreparationMethods_)
                {
                    Pattern = PreparePattern(Pattern);
                }
                foreach (ProcessDataMethod FindPattern in LookupMethods_)
                {
                    if (FindPattern.ProcessingMethod(Data, Pattern))
                    {
                        return PatternFound;
                    }
                }
                return PatternNotFound;
            }

            private string CreateObjects(Product ProsessedProduct, List<Listing> MatchedListings)
            {
                StringBuilder Output = new StringBuilder();
                Output.Append(string.Concat("{",String.Format("\r\n\t\"product_name\": \"{0}\",\r\n",ProsessedProduct.Name.ToString())));
                Output.Append("\t\"listings\": [");
                foreach(Listing CurrentListing in MatchedListings)
                {
                    Output.Append(CurrentListing.ToString());
                    Output.Append(",");
                }
                Output.Length--;
                Output.Append("]\r\n},");
                return Output.ToString();
            }
        }
        public class ListOfProducts
        {
            List<Product> ListOfProducts_ = new List<Product>();
            public ListOfProducts(string FileName)
            {
                if (!File.Exists(FileName))
                {
                    Exit(String.Concat("Products file does not exist! (",FileName,")"));
                }
                string[] DataFromFile = File.ReadAllLines(FileName);
                foreach (string DataLine in DataFromFile)
                {
                    ListOfProducts_.Add(new Product(DataLine));
                }
            }
            public List<Product> ProductList
            {
                get { return ListOfProducts_; }
            }
        }
        public class Listing
        {
            private string title_;
            private string manufacturer_;
            private string currency_;
            private float price_;
            public Listing(string listingString)
            {
                string[,] ListingFields = ParseFields(listingString);
                int DataFieldsNumber = ListingFields.GetLength(1);
                for (int i = 0; i < DataFieldsNumber; i++ )
                {
                    switch (ListingFields[0,i])
                    {
                        case "title":
                            title_ = ListingFields[1,i];
                            break;
                        case "manufacturer":
                            manufacturer_ = ListingFields[1, i];
                            break;
                        case "currency":
                            currency_ = ListingFields[1, i];
                            break;
                        case "price":
                            price_ = (float)Convert.ToDecimal(ListingFields[1, i]);
                            break;
                    }
                }
            }
            public string Title
            {
                get { return title_; }
                set { title_ = value; }
            }
            public string Manufacturer
            {
                get { return manufacturer_; }
                set { manufacturer_ = value; }
            }
            public string Currency
            {
                get { return currency_; }
                set { currency_ = value; }
            }
            public float Price
            {
                get { return price_; }
                set { price_ = value; }
            }
            public override string ToString()
            {
                return String.Concat("{",String.Format(" \"title\": \"{0}\",\"manufacturer\": \"{1}\",\"currency\": \"{2}\", \"price\": \"{3}\"",title_,manufacturer_,currency_,price_.ToString("F2")),"}");
            }

        }
        public class Product
        {
            private string productName_;
            private string manufacturer_;
            private string model_;
            private string family_;
            private DateTime announcedDate_;
            public Product(string productString)
            {
                string[,] ProductFields = ParseFields(productString);
                int DataFieldsNumber = ProductFields.GetLength(1);
                for (int i = 0; i < DataFieldsNumber; i++)
                {
                    switch (ProductFields[0, i])
                    {
                        case "product_name":
                            productName_ = ProductFields[1, i];
                            break;
                        case "manufacturer":
                            manufacturer_ = ProductFields[1, i];
                            break;
                        case "model":
                            model_ = ProductFields[1, i];
                            break;
                        case "family":
                            family_ = ProductFields[1, i];
                            break;
                        case "announced-date":
                            announcedDate_ = Convert.ToDateTime(ProductFields[1, i]);
                            break;
                    }
                }
            }
            public string Name
            {
                get { return productName_; }
                set { productName_ = value; }
            }
            public string Manufacturer
            {
                get { return manufacturer_; }
                set { manufacturer_ = value; }
            }
            public string Model
            {
                get { return model_; }
                set { model_ = value; }
            }
            public string Family
            {
                get { return family_; }
                set { family_ = value; }
            }
            public DateTime AnnouncedDate
            {
                get { return announcedDate_; }
                set { announcedDate_ = value; }
            }
        }
        public class ProcessDataMethod
        {
            private PatternLookupMethod ProcessingMethod_;
            public ProcessDataMethod( PatternLookupMethod method)
            {
                if (method == null) throw new Exception("New Process data method", new Exception("Method can not be null!"));
                ProcessingMethod_ = method;
            }
            public PatternLookupMethod ProcessingMethod
            {
                get 
                {
                    if (ProcessingMethod_ == null)
                    {
                        throw new Exception("Executing Pattern Lookup Method", new Exception("Pattern look up method is null!"));
                    } 
                    return ProcessingMethod_;
                }
            }
        }
        public static void Exit(string message)
        {
            Console.WriteLine(message);
            Console.ReadLine();
            Environment.Exit(0);
        }
        public static string[,] ParseFields(string fields)
        {
            int QuotesCount = 0;
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i] == '"')
                {
                    QuotesCount++;
                }
            }
            int[] QuotesPositions = new int[QuotesCount];
            QuotesCount = 0;
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i] == '"')
                {
                    QuotesPositions[QuotesCount++] = i;
                }
            }
            int DataFieldsNumber = QuotesPositions.Length / 4;
            string[,] ProductFields = new string[2, DataFieldsNumber];

            for (int i = 0; i < DataFieldsNumber; i++)
            {
                ProductFields[0, i] = fields.Substring(QuotesPositions[4 * i] + 1, (QuotesPositions[4 * i + 1] - QuotesPositions[4 * i] - 1));
                ProductFields[1, i] = fields.Substring(QuotesPositions[4 * i + 2] + 1, (QuotesPositions[4 * i + 3] - QuotesPositions[4 * i + 2] - 1));
            }

            return ProductFields;
        }
        public static bool DirectScanRegex(string data, string pattern)
        {
            bool PatternFound = true;
            bool PatternNotFound = false;

            if(Regex.IsMatch(data, pattern))
            {
                return PatternFound;
            }
            return PatternNotFound;
        }
        public static string ConvertPattern(string input)
        {
            char[ ] ReplacementChars = {' ', ',', '-', '/', ':', '_'};
            string Output = input;
            foreach (char symbol in ReplacementChars)
            {
                Output = Output.Replace(symbol.ToString(), ".{0,1}");
            }
            Output = String.Concat(Output, ' ');
            return Output;
        }
    }
}
