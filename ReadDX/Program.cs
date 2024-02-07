using Autodesk.DataExchange;
using Autodesk.DataExchange.Authentication;
using Autodesk.DataExchange.Core.Enums;
using Autodesk.DataExchange.Core.Interface;
using Autodesk.DataExchange.Core.Models;
using Autodesk.DataExchange.DataModels;
using Autodesk.DataExchange.Extensions.HostingProvider;
using Autodesk.DataExchange.Extensions.Logging.File;
using Autodesk.DataExchange.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Parameter = Autodesk.DataExchange.DataModels.Parameter;

namespace HelloDXCLI
{
    namespace ReadDX
    {
        internal class Program
        {
            static private Auth auth = null;
            static private ILogger logger = null;
            static private IHostingProvider host = null;

            class ExchangeReader
            {
                private string currentRevision;
                private ExchangeData currentExchangeData;
                public ExchangeReader(Client client) { }
                /***
                 * PrintMessage out the parameters by label and detail if provided, and the downloaded geometry file path. 
                 * If no parameter names are provided, the method will not print any parameters.
                 */
                public async Task GetLatestExchangeDetailsAsync(DataExchangeIdentifier exchangeIdentifier, List<string> paramNames = null)
                {
                    try
                    {
                        //Get a list of all revisions
                        var revisions = await client.GetExchangeRevisionsAsync(exchangeIdentifier);
                        //Get the latest revision
                        var firstRev = revisions.First().Id;

                        if (!string.IsNullOrEmpty(currentRevision) && currentRevision == firstRev)
                        {
                            Console.WriteLine("No changes found");
                            return;
                        }

                        // Get Exchange data
                        if (currentExchangeData == null || currentExchangeData?.ExchangeID != exchangeIdentifier.ExchangeId)
                        {
                            // Get full Exchange Data till the latest revision
                            currentExchangeData = await client.GetExchangeDataAsync(exchangeIdentifier);
                            currentRevision = firstRev;

                            // Use Revit Wrapper
                            var data = ElementDataModel.Create(client, currentExchangeData);
                            var flattenedListOfElements = GetFlattenedListOfElements(data.Elements);

                            flattenedListOfElements
                                .Where(element => paramNames != null && paramNames.Count > 0)
                                .ToList()
                                .ForEach(element =>
                                {
                                    var elementName = element.Name;
                                    var categoryName = element.Category;
                                    var familyName = element.Family;
                                    var typeName = element.Type;
                                    PrintMessage("Element", categoryName);
                                    PrintMessage("Category", categoryName);
                                    PrintMessage("Family", familyName);
                                    PrintMessage("Type", typeName);
                                    // DX SDK is working on providing APIs for Type Parameters. For now, this is a workaround to get the parameters include the "Type Parameters" for an element.
                                    var parameters = element.TypeParameters
                                        .Concat(element.InstanceParameters).Concat(element.ModelStructureParameters)
                                        .ToList();

                                    parameters.ForEach(parameter => ShowParameter(parameter));
                                });
                            var allGeometries = await data.GetElementGeometriesByElementsAsync(flattenedListOfElements).ConfigureAwait(false);
                            // PrintMessage out the geometry file path
                            var wholeGeometryPathOBJ = Client.DownloadCompleteExchangeAsOBJ(exchangeIdentifier.ExchangeId, exchangeIdentifier.CollectionId);
                            PrintMessage("Geometry file path", wholeGeometryPathOBJ);
                        }
                    }
                    catch (Exception e)
                    {
                        PrintMessage("An error occurred while reading Exchange", e.Message);
                    }
                }
            }
            public static class AppConfig
            {

                public static string AuthClientId = System.Configuration.ConfigurationManager.AppSettings["AuthClientID"];
                public static string AuthClientSecret = System.Configuration.ConfigurationManager.AppSettings["AuthClientSecret"];
                public static string AuthCallBack = System.Configuration.ConfigurationManager.AppSettings["AuthCallBack"];

                // The location in ACC in which all the Data Exchanges produced by this app will be stored.
                public static string Region = "US";
                // You can get the HubUrn, ProjectUrn and FolderUrn using Data Exchange API using: https://aps-dx-explorer.autodesk.io/
                // Construction Records Testing
                public static string HubUrn = "b.768cae14-76b3-4531-9030-25212dab4e48";
                // Data Exchange API Private Beta
                public static string ProjectUrn = "b.d7617730-85af-494f-9105-9b16425b7e97";
                // Project Files > SupportArea > Geng > FDX
                public static string FolderUrn = "urn:adsk.wipprod:fs.folder:co.xGvjBe8bSZqaeQp7PNd3_w";
                // Link
                // https://acc.autodesk.com/docs/files/projects/d7617730-85af-494f-9105-9b16425b7e97?folderUrn=urn%3Aadsk.wipprod%3Afs.folder%3Aco.xGvjBe8bSZqaeQp7PNd3_w&viewModel=detail&moduleId=folders

                public static string AppName = "ReadDX";
                public static string AppBasePath { get => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName); }
                public static string LogDirectoryPath { get => Path.Combine(AppBasePath, "logs"); }

                public static List<string> ExchangeUrls = new List<string>
                {
                    "https://acc.autodesk.com/build/files/projects/d7617730-85af-494f-9105-9b16425b7e97?folderUrn=urn%3Aadsk.wipprod%3Afs.folder%3Aco.xGvjBe8bSZqaeQp7PNd3_w&entityId=urn%3Aadsk.wipprod%3Adm.lineage%3A5EALmtiHR6i9K9V38qGXZg&viewableGuid=3c25a776-fbf1-464d-81f0-372def569eec",
                    "https://acc.autodesk.com/docs/files/projects/d7617730-85af-494f-9105-9b16425b7e97?folderUrn=urn%3Aadsk.wipprod%3Afs.folder%3Aco.xGvjBe8bSZqaeQp7PNd3_w&entityId=urn%3Aadsk.wipprod%3Adm.lineage%3A72uQk1vaT7iCM--gN80hDw&viewModel=detail&moduleId=folders&viewableGuid=a0db901a-6e12-4521-b15d-15eec3698c2b", // a beam with two parameters
                    "https://acc.autodesk.com/build/files/projects/d7617730-85af-494f-9105-9b16425b7e97?folderUrn=urn:adsk.wipprod:fs.folder:co.xGvjBe8bSZqaeQp7PNd3_w&entityId=urn:adsk.wipprod:dm.lineage:0KolYlOAS6WDetFsurc7Zg", //sphere mesh (3x3) created using GH\r\n
                    "https://acc.autodesk.com/docs/files/projects/d7617730-85af-494f-9105-9b16425b7e97?folderUrn=urn%3Aadsk.wipprod%3Afs.folder%3Aco.xGvjBe8bSZqaeQp7PNd3_w&entityId=urn%3Aadsk.wipprod%3Adm.lineage%3AmRxViu9QTairm2CTVRDudg&viewModel=detail&moduleId=folders", // RVT2024_walls_and_furnitures
                };
                // List of parameter names to filter
                public static List<string> ParameterNames = new List<string> { "Phase Created", "FireRating" };
            }

            private static readonly object padlock = new object();

            static private Client client = null;
            // When you first access this property, it will authenticate you for APS services using 3LO.
            static public Client Client
            {
                get
                {
                    lock (padlock)
                    {
                        if (client == null)
                        {
                            var sdkOptions = new SDKOptionsDefaultSetup()
                            {
                                ApplicationName = AppConfig.AppName,
                                ClientId = AppConfig.AuthClientId,
                                ClientSecret = AppConfig.AuthClientSecret,
                                CallBack = AppConfig.AuthCallBack,
                            };

                            client = new Client(sdkOptions);
                            sdkOptions.Logger.SetDebugLogLevel();
                            client.EnableHttpDebugLogging();

                            auth = new Auth(new AuthOptions()
                            {
                                CallBack = AppConfig.AuthCallBack,
                                ClientId = AppConfig.AuthClientId,
                                ClientSecret = AppConfig.AuthClientSecret,
                            });

                            logger = new Log(AppConfig.LogDirectoryPath);

                            host = new ACC(logger, () => auth.GetAuthToken());
                        }
                        return client;
                    }
                }
            }

            static async Task Main(string[] args)
            {
                // Init the client.
                _ = Client;

                string url = AppConfig.ExchangeUrls[0];
                PrintMessage("Exchange URL", url);
                Console.WriteLine();

                DataExchangeIdentifier exchangeIdentifier = await ExchangeFromUrl(url);
                var exchageReader = new ExchangeReader(Client);
                await exchageReader.GetLatestExchangeDetailsAsync(exchangeIdentifier, AppConfig.ParameterNames);
            }

            #region Utility 

            static async Task<List<ElementGeometry>> GetGeometryByElementAsync(ElementDataModel model, Element element)
            {
                List<ElementGeometry> geometryList = await model.GetElementGeometryByElementAsync(element);

                // Use LINQ's ForEach method to perform an action for each element in the list
                geometryList.ForEach(elGeometry =>
                {
                    Console.WriteLine("id: {0}; length unit: {1}", elGeometry.Id, elGeometry.LengthUnit);
                });

                return geometryList;
            }


            static async Task<Dictionary<Element, IEnumerable<ElementGeometry>>> GetGeometriesByElemenstAsync(ElementDataModel model, IEnumerable<Element> elements)
            {
                var result = await model.GetElementGeometriesByElementsAsync(elements);

                // Perform the action for each key-value pair in the dictionary using LINQ's Select method
                var processedResult = result.Select(kvp =>
                {
                    Element element = kvp.Key;
                    IEnumerable<ElementGeometry> geometries = kvp.Value;
                    geometries.ToList().ForEach(geometry => Console.WriteLine(geometry.LengthUnit));
                    return kvp; // Return the original key-value pair
                }).ToDictionary(kvp => kvp.Key, kvp => kvp.Value); // Convert the processed results back to a dictionary

                return processedResult; // Return the processed dictionary
            }

            static async Task<DataExchangeIdentifier> ExchangeFromUrl(string url)
            {
                // Parse the query string from the URL
                var queryString = new Uri(url).Query;

                // Parse the query parameters
                var parsedQuery = HttpUtility.ParseQueryString(queryString);

                // Retrieve the detail of the "q" parameter
                string exchangeId = parsedQuery["entityId"];

                var exchangeDetails = await Client.GetExchangeDetailsAsync(exchangeId).ConfigureAwait(false);

                var ExchangeIdentifier = new DataExchangeIdentifier
                {
                    HubId = AppConfig.HubUrn,
                    //HubId = exchangeDetails.HubId,
                    CollectionId = exchangeDetails.CollectionID,
                    ExchangeId = exchangeDetails.ExchangeID
                };

                return ExchangeIdentifier;
            }

            private async Task<List<ElementGeometry>> GetAllElementGeometriesAsync(IEnumerable<Element> elements, ElementDataModel dataModel)
            {
                var allGeometries = await Task.WhenAll(elements.Select(async element =>
                {
                     var elementGeometries = await dataModel.GetElementGeometryByElementAsync(element);
                     return elementGeometries;
                }));

                // Flatten the list of lists into one list
                return allGeometries.SelectMany(x => x).ToList();
            }

            // GetFlattenedListOfElements method gets all the parent and child elements of an exchange
            internal static IEnumerable<Element> GetFlattenedListOfElements(IEnumerable<Element> elements)
            {
                return elements.Concat(elements.SelectMany(element => GetFlattenedListOfElements(element.GetChildElements())));
            }

            private static void ShowParameter(Parameter parameter)
            {
                if (parameter.ParameterDataType == ParameterDataType.ParameterSet)
                {
                    (parameter as ParameterSet)?.Parameters?.ToList()?.ForEach(ShowParameter);
                }
                else
                {
                    PrintMessage(parameter.Name, parameter.Value);
                }
            }

            private static void PrintMessage(string label, dynamic detail)
            {
                Console.WriteLine($"{label}: \n{detail}");
                // Print a horizontal divider
                Console.WriteLine(new string('-', 72));
            }
            #endregion
        }
    }
}