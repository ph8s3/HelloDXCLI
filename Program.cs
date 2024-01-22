using Autodesk.DataExchange;
using Autodesk.DataExchange.Authentication;
using Autodesk.DataExchange.ContractProvider;
using Autodesk.DataExchange.Core.Enums;
using Autodesk.DataExchange.Core.Interface;
using Autodesk.DataExchange.Core.Models;
using Autodesk.DataExchange.DataModels;
using Autodesk.DataExchange.Extensions.HostingProvider;
using Autodesk.DataExchange.Extensions.Logging.File;
using Autodesk.DataExchange.Models;
using Autodesk.Parameters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using PrimitiveGeometry = Autodesk.GeometryPrimitives;

namespace HelloDXCLI
{
    internal class Program
    {
        static private Auth auth = null;
        static private ILogger logger = null;
        static private IHostingProvider host = null;
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

            public static string AppName = "HelloDXCLI";
            public static string AppBasePath { get => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName); }
            public static string LogDirectoryPath { get => Path.Combine(AppBasePath, "logs"); }
        }

        internal class ExchangeConfig
        {
            public ExchangeConfig() { }

            public string RevitCategory { get; set; } = "Generic Models";
            public string RevitFamily { get; set; } = "Generic Models";
            public string RevitFamilyType { get; set; } = "Generic Models";
            public string GeometryfileName { get; set; } = "empty";
            public string GeometryFilePath { get => asssetsPath + GeometryfileName; }

            private static string asssetsPath = $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\\SampleModels\\";

            public RenderStyle DefaultRenderStyle
            {
                get => new RenderStyle()
                {
                    Name = "defaultRenderStyle",
                    RGBA = new RGBA()
                    {
                        Red = 0,
                        Blue = 0,
                        Green = 0,
                        Alpha = 255
                    },
                    Transparency = 255
                };
            }
            // Whenever an Exchange container is created, it's given this canned name.
            public string ExchangeName
            {
                get
                {
                    string namePrefix = "hello-dx-cli";
                    var datetime = DateTime.Now.ToString("MM-dd-yyyy-h-mm-tt-ss");
                    //return namePrefix + "-" + GeometryfileName + "-" + datetime;
                    return namePrefix + "-" + datetime;
                }
            }
            public static double STEPTolerance { get; set; } = 0.001;
        }
        // ifc
        private static readonly ExchangeConfig sampleBeamModel = new ExchangeConfig()
        {
            GeometryfileName = "beam.ifc",
            RevitCategory = "Structural Beam System",
            RevitFamily = "UB-Universal Beams:UB305x165x40",
            RevitFamilyType = "UB-Universal Beams:UB305x165x40"
        };
        // ifc. This doesn't work because currently "multi-part" models in a single file is not supported.
        private static readonly ExchangeConfig sampleColumnModel = new ExchangeConfig()
        {
            GeometryfileName = "rst_advanced_sample_project_structural_columns.ifc",
            RevitCategory = "Structural Columns",
            RevitFamily = "M_Concrete-Round-Column",
            RevitFamilyType = "450mm"
        };
        // obj
        private static readonly ExchangeConfig sampleWarehouseModel = new ExchangeConfig()
        {
            GeometryfileName = "warehouse.obj",
            RevitCategory = "Generic Model",
            RevitFamily = "Generic Model",
            RevitFamilyType = "Generic Model"
        };
        // stp
        private static readonly ExchangeConfig sampleBoxModel = new ExchangeConfig()
        {
            GeometryfileName = "box.stp"
        };

        private static Dictionary<string, ExchangeConfig> sampleModels = new Dictionary<string, ExchangeConfig>
        {
            {"ifc", sampleBeamModel},
            {"obj", sampleWarehouseModel},
            {"stp", sampleBoxModel},
        };

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
            await SendAnExchange();
        }
        
        private static async Task SendAnExchange()
        {
            /*Random random = new Random();
            List<string> keys = new List<string>(sampleModels.Keys);
            string key = keys[random.Next(keys.Count)];
            ExchangeConfig exchangeConfig = sampleModels[key];*/

            ExchangeConfig exchangeConfig = sampleBeamModel;
            
            await SendDataExchange(exchangeConfig);
        }

        // Randomly pick a 3D model in the sampleModel folder and send to an Exchange.
        private static async Task SendDataExchange(ExchangeConfig exchangeConfig)
        {
            var (exchangeIdentifier, exchangeDetails) = await CreateExchangeContainer(exchangeConfig.ExchangeName);
            var exchangeData = await CreateExchangeData(exchangeConfig, exchangeIdentifier, exchangeDetails);
            //var exchangeData = CreateCircleAsExchangeData(exchangeIdentifier);
            
            await PublishExchangeData(exchangeIdentifier, exchangeDetails, exchangeData);
            string exchangeId = exchangeDetails.ExchangeID;
            string exchangeUrn = exchangeDetails.FileUrn;
            string exchangeName = exchangeDetails.DisplayName;
            string exchangeUrl = UrlToExchange(exchangeDetails);
            
            Console.WriteLine("|-----------------------------------------------------------------");
            Console.WriteLine("| Your Data Exchange has been created:");
            Console.WriteLine("|   Name   :  {0}", exchangeName);
            Console.WriteLine("|   ID     :  {0}", exchangeId);
            Console.WriteLine("|   URN    :  {0}", exchangeUrn);
            Console.WriteLine("|   URL    :  {0}", exchangeUrl);
            Console.WriteLine("|-----------------------------------------------------------------");
        }

        // Create an Exchange container at the location in ACC specified by AppConfig
        async static Task<(DataExchangeIdentifier exchangeIdentifier, ExchangeDetails exchangeDetails)> CreateExchangeContainer(string exchangeName)
        {
            var exchangeCreateRequest = new ExchangeCreateRequestACC()
            {
                Host = Client.SDKOptions.HostingProvider,
                Contract = new ContractProvider(),
                FileName = exchangeName,
                Description = string.Empty,
                ACCFolderURN = AppConfig.FolderUrn,
                ProjectId = AppConfig.ProjectUrn,
                HubId = AppConfig.HubUrn,
                Region = AppConfig.Region,
            };

            var exchangeDetails = await Client.CreateExchangeAsync(exchangeCreateRequest);
            
            // The DataExchangeIdentifier will be used to identify the exchange for further api calls. 
            DataExchangeIdentifier exchangeIdentifier = new DataExchangeIdentifier
            {
                CollectionId = exchangeDetails.CollectionID,
                ExchangeId = exchangeDetails.ExchangeID,
                HubId = exchangeDetails.HubId,
            };
            return (exchangeIdentifier, exchangeDetails);
        }

        // Packs the Exchange Data to be published to the Data Exchange container
        private static async Task<ExchangeData> CreateExchangeData(ExchangeConfig exchangeConfig, DataExchangeIdentifier exchangeIdentifier, ExchangeDetails exchangeDetails)
        {
            var exchangeData = await Client.GetExchangeDataAsync(exchangeIdentifier);
            var revitExchangeData = ElementDataModel.Create(Client, exchangeData);

            // Create a Revit element.
            var element = revitExchangeData.AddElement(new ElementProperties(Guid.NewGuid().ToString(), exchangeConfig.RevitCategory, exchangeConfig.RevitFamily, exchangeConfig.RevitFamilyType));
            element.Name = "Beam 1";
            // Get geometry for the element. In this example, we are using canned files resides at "SampleModels" folder. As of writing,.OBJ, .IFC, and/or .STP files are supported.
            // Geometry primitives such as points, lines, polylines are supported: handy for drawing 2D plans or abstract representation of your detailed geometry
            var geometryFilePath = exchangeConfig.GeometryFilePath;
            var geometry = ElementDataModel.CreateGeometry(new GeometryProperties(geometryFilePath, exchangeConfig.DefaultRenderStyle));
            var elementGeometries = new List<ElementGeometry> { geometry };
            revitExchangeData.SetElementGeometryByElement(element, elementGeometries);

            // Create a Forge built-in parameterPhase (Phase) for the element (instance):
            // You can verify that the instance parameterPhase has been created using Data Exchange Model Explorer at https://aps-dx-explorer.autodesk.io/
            // And select the instance
            ParameterDefinition parameterPhase = ParameterDefinition.Create(Autodesk.Parameters.Parameter.PhaseCreated, ParameterDataType.String);
            parameterPhase.IsTypeParameter = false; // instance parameterPhase
            ((StringParameterDefinition)parameterPhase).Value = "Phase 2";
            await element.CreateParameterAsync(parameterPhase);

            // Create a custom parameterPhase of the element.
            var SchemaId = "exchange.parameter." + exchangeDetails.SchemaNamespace + ":String" + "EUFireRating" + "TestCustomParameter-1.0.0";
            ParameterDefinition parameterFireRating = ParameterDefinition.Create(SchemaId, ParameterDataType.String);
            parameterFireRating.Name = "FireRating" + Guid.NewGuid();
            parameterFireRating.SampleText = "A fire rating class";
            parameterFireRating.Description = "Fire rating provided by manufacturer";
            parameterFireRating.ReadOnly = false;
            parameterFireRating.IsTypeParameter = false;
            parameterFireRating.GroupID = Group.General.DisplayName();
            ((StringParameterDefinition)parameterFireRating).Value = "A1";
            await element.CreateParameterAsync(parameterFireRating);

            return revitExchangeData.ExchangeData;
        }

        private static ExchangeData CreateCircleAsExchangeData(DataExchangeIdentifier dataExchangeIdentifier)
        {
            var exchangeData = Client.GetExchangeDataAsync(dataExchangeIdentifier);
            exchangeData.Wait();
            var revitExchangeData = ElementDataModel.Create(Client, exchangeData.Result);
            var circleElement = revitExchangeData.AddElement(new ElementProperties(Guid.NewGuid().ToString(), "Circle", "Circle", "Circle"));
            var newPointElementGeometry = new List<ElementGeometry>();
            Random random = new Random();
            var center = new PrimitiveGeometry.Math.Point3d(random.Next(999), random.Next(999), random.Next(999));
            var normal = new PrimitiveGeometry.Math.Vector3d(0, 0, 1);
            var radius = new PrimitiveGeometry.Math.Vector3d(random.Next(50), 0, 0);
            var circle = new PrimitiveGeometry.Geometry.Circle(center, normal, radius);
            newPointElementGeometry.Add(ElementDataModel.CreatePrimitiveGeometry(new GeometryProperties(circle, new RenderStyle()
            {
                Name = "defaultRenderStyle",
                RGBA = new RGBA()
                {
                    Red = 255,
                    Blue = 0,
                    Green = 0,
                    Alpha = 255
                },
                Transparency = 0
            })));
            revitExchangeData.SetElementGeometryByElement(circleElement, newPointElementGeometry);
            return revitExchangeData.ExchangeData;

        }

        // Send Exchange Data to ACC. After the success completion of the task, you can access the Data Exchange in ACC Docs and Viewer.
        private static async Task PublishExchangeData(DataExchangeIdentifier dataExchangeIdentifier, ExchangeDetails exchangeDetails, ExchangeData exchangeData)
        {
            await Client.SyncExchangeDataAsync(dataExchangeIdentifier, exchangeData);
            //// Exception: Autodesk.DataExchange.OpenAPI.ApiException`1: 'The request is invalid. Status: 400 Response:
            await Client.GenerateViewableAsync(
                //exchangeDetails.DisplayName, 
                exchangeDetails.ExchangeID,
                exchangeDetails.CollectionID
                //exchangeDetails.FileUrn
                );
        }
        // Utility 
        static string UrlToExchange(ExchangeDetails exchangeDetails)
        {
            string baseUrl = "https://acc.autodesk.com/build/files/projects/";

            string projectUrn = exchangeDetails.ProjectUrn;
            string folderUrn = exchangeDetails.FolderUrn;
            string fileUrn = exchangeDetails.FileUrn;
            string projectUrn1 = projectUrn.StartsWith("b.") ? projectUrn.Replace("b.", "") : projectUrn;
            string link = String.Concat(baseUrl, projectUrn1, "?folderUrn=", folderUrn, "&entityId=", fileUrn);
            return link;
        }
    }
}
