﻿using Common.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web.Hosting;
using VstsDemoBuilder.Extensions;
using VstsDemoBuilder.Models;
using VstsDemoBuilder.ServiceInterfaces;
using VstsRestAPI;
using VstsRestAPI.ExtensionManagement;
using VstsRestAPI.Extractor;
using VstsRestAPI.ProjectsAndTeams;
using VstsRestAPI.QueriesAndWidgets;
using VstsRestAPI.Service;
using VstsRestAPI.Viewmodel.Extractor;
using Parameters = VstsRestAPI.Viewmodel.Extractor.GetServiceEndpoints;

namespace VstsDemoBuilder.Services
{
    public class ExtractorService : IExtractorService
    {
        #region STATIC DECLARATIONS
        public static ILog logger = LogManager.GetLogger("ErrorLog");
        public static readonly object objLock = new object();
        public static Dictionary<string, string> statusMessages;
        public static List<string> errorMessages = new List<string>();
        public static string extractedTemplatePath = string.Empty;
        public static void AddMessage(string id, string message)
        {
            lock (objLock)
            {
                if (id.EndsWith("_Errors"))
                {
                    StatusMessages[id] = (StatusMessages.ContainsKey(id) ? StatusMessages[id] : string.Empty) + message;
                }
                else
                {
                    StatusMessages[id] = message;
                }
            }
        }
        public static Dictionary<string, string> StatusMessages
        {
            get
            {
                if (statusMessages == null)
                {
                    statusMessages = new Dictionary<string, string>();
                }

                return statusMessages;
            }
            set
            {
                statusMessages = value;
            }
        }
        public static string GetStatusMessage(string id)
        {
            lock (objLock)
            {
                string message = string.Empty;
                if (StatusMessages.Keys.Count(x => x == id) == 1)
                {
                    message = StatusMessages[id];
                }
                else
                {
                    return "100";
                }

                if (id.EndsWith("_Errors"))
                {
                    RemoveKey(id);
                }

                return message;
            }
        }

        public static void RemoveKey(string id)
        {
            lock (objLock)
            {
                StatusMessages.Remove(id);
            }
        }

        #endregion  STATIC DECLARATIONS

        #region ANALYSIS - GET COUNTS
        public ProjectConfigurations ProjectConfiguration(Project model)
        {
            string repoVersion = System.Configuration.ConfigurationManager.AppSettings["RepoVersion"];
            string buildVersion = System.Configuration.ConfigurationManager.AppSettings["BuildVersion"];
            string releaseVersion = System.Configuration.ConfigurationManager.AppSettings["ReleaseVersion"];
            string wikiVersion = System.Configuration.ConfigurationManager.AppSettings["WikiVersion"];
            string boardVersion = System.Configuration.ConfigurationManager.AppSettings["BoardVersion"];
            string workItemsVersion = System.Configuration.ConfigurationManager.AppSettings["WorkItemsVersion"];
            string releaseHost = System.Configuration.ConfigurationManager.AppSettings["ReleaseHost"];
            string defaultHost = System.Configuration.ConfigurationManager.AppSettings["DefaultHost"];
            string extensionHost = System.Configuration.ConfigurationManager.AppSettings["ExtensionHost"];
            string getReleaseVersion = System.Configuration.ConfigurationManager.AppSettings["GetRelease"];
            string agentQueueVersion = System.Configuration.ConfigurationManager.AppSettings["AgentQueueVersion"];
            string extensionVersion = System.Configuration.ConfigurationManager.AppSettings["ExtensionVersion"];
            string endpointVersion = System.Configuration.ConfigurationManager.AppSettings["EndPointVersion"];
            string queriesVersion = System.Configuration.ConfigurationManager.AppSettings["QueriesVersion"];
            ProjectConfigurations projectConfig = new ProjectConfigurations();

            projectConfig.AgentQueueConfig = new Configuration() { UriString = defaultHost + model.accountName + "/", PersonalAccessToken = model.accessToken, Project = model.ProjectName, AccountName = model.accountName, Id = model.id, VersionNumber = wikiVersion };
            projectConfig.WorkItemConfig = new Configuration() { UriString = defaultHost + model.accountName + "/", PersonalAccessToken = model.accessToken, Project = model.ProjectName, AccountName = model.accountName, Id = model.id, VersionNumber = wikiVersion };
            projectConfig.BuildDefinitionConfig = new Configuration() { UriString = defaultHost + model.accountName + "/", PersonalAccessToken = model.accessToken, Project = model.ProjectName, AccountName = model.accountName, Id = model.id, VersionNumber = buildVersion };
            projectConfig.ReleaseDefinitionConfig = new Configuration() { UriString = releaseHost + model.accountName + "/", PersonalAccessToken = model.accessToken, Project = model.ProjectName, AccountName = model.accountName, Id = model.id, VersionNumber = releaseVersion };
            projectConfig.RepoConfig = new Configuration() { UriString = defaultHost + model.accountName + "/", PersonalAccessToken = model.accessToken, Project = model.ProjectName, AccountName = model.accountName, Id = model.id, VersionNumber = repoVersion };
            projectConfig.BoardConfig = new Configuration() { UriString = defaultHost + model.accountName + "/", PersonalAccessToken = model.accessToken, Project = model.ProjectName, AccountName = model.accountName, Id = model.id, VersionNumber = boardVersion };
            projectConfig.Config = new Configuration() { UriString = defaultHost + model.accountName + "/", PersonalAccessToken = model.accessToken, Project = model.ProjectName, AccountName = model.accountName, Id = model.id };
            projectConfig.GetReleaseConfig = new Configuration() { UriString = releaseHost + model.accountName + "/", PersonalAccessToken = model.accessToken, Project = model.ProjectName, AccountName = model.accountName, Id = model.id, VersionNumber = getReleaseVersion };
            projectConfig.ExtensionConfig = new Configuration() { UriString = extensionHost + model.accountName + "/", PersonalAccessToken = model.accessToken, Project = model.ProjectName, AccountName = model.accountName, Id = model.id, VersionNumber = extensionVersion };
            projectConfig.EndpointConfig = new Configuration() { UriString = defaultHost + model.accountName + "/", PersonalAccessToken = model.accessToken, Project = model.ProjectName, AccountName = model.accountName, Id = model.id, VersionNumber = endpointVersion };
            projectConfig.QueriesConfig = new Configuration() { UriString = defaultHost + model.accountName + "/", PersonalAccessToken = model.accessToken, Project = model.ProjectName, AccountName = model.accountName, Id = model.id, VersionNumber = queriesVersion };

            return projectConfig;
        }
        public int GetTeamsCount(ProjectConfigurations appConfig)
        {
            VstsRestAPI.Extractor.ClassificationNodes nodes = new VstsRestAPI.Extractor.ClassificationNodes(appConfig.BoardConfig);
            TeamList teamList = nodes.ExportTeamList("");
            int count = 0;
            if (teamList.value != null)
            {
                count = teamList.value.Count;
            }
            return count;
        }
        public int GetIterationsCount(ProjectConfigurations appConfig)
        {
            VstsRestAPI.Extractor.ClassificationNodes nodes = new VstsRestAPI.Extractor.ClassificationNodes(appConfig.BoardConfig);
            GetINumIteration.Iterations iterations = new GetINumIteration.Iterations();
            iterations = nodes.GetiterationCount();
            if (iterations.count > 0)
            {
                return iterations.count;
            }
            else
            {
                if (!(string.IsNullOrEmpty(nodes.LastFailureMessage)))
                {
                    errorMessages.Add("Error while fetching iteration(s) count: " + nodes.LastFailureMessage);
                }
                return 0;
            }
        }
        public int GetBuildDefinitionCount(ProjectConfigurations appConfig)
        {
            int BuildDefCount = 0;
            BuildandReleaseDefs buildandReleaseDefs = new BuildandReleaseDefs(appConfig.BuildDefinitionConfig);
            GetBuildDefResponse.BuildDef buildDef = new GetBuildDefResponse.BuildDef();
            buildDef = buildandReleaseDefs.GetBuildDefCount();
            if (buildDef.count > 0)
            {
                BuildDefCount = buildDef.count;
            }
            else if (!string.IsNullOrEmpty(buildandReleaseDefs.LastFailureMessage))
            {
                errorMessages.Add("Error while fetching build definition count: " + buildandReleaseDefs.LastFailureMessage);
            }
            return BuildDefCount;
        }
        public int GetReleaseDefinitionCount(ProjectConfigurations appConfig)
        {
            int ReleaseDefCount = 0;
            BuildandReleaseDefs buildandReleaseDefs = new BuildandReleaseDefs(appConfig.ReleaseDefinitionConfig);
            GetReleaseDefResponse.ReleaseDef releaseDef = new GetReleaseDefResponse.ReleaseDef();
            releaseDef = buildandReleaseDefs.GetReleaseDefCount();
            if (releaseDef.count > 0)
            {
                ReleaseDefCount = releaseDef.count;
            }
            else if (!string.IsNullOrEmpty(buildandReleaseDefs.LastFailureMessage))
            {
                errorMessages.Add("Error while fetching release definition count: " + buildandReleaseDefs.LastFailureMessage);
            }
            return ReleaseDefCount;
        }
        #endregion ANALYSIS - GET COUNTS

        #region GENERATE ARTIFACTS
        public string[] GenerateTemplateArifacts(Project model)
        {
            extractedTemplatePath = HostingEnvironment.MapPath("~") + @"ExtractedTemplate\";
            AddMessage(model.id, "");
            ProjectConfigurations appConfig = ProjectConfiguration(model);

            GetInstalledExtensions(appConfig);

            ExportQuries(appConfig);
            ExportTeams(appConfig.BoardConfig, model.ProcessTemplate, model.ProjectId);

            if (ExportIterations(appConfig))
            {
                AddMessage(model.id, "Iterations Definition");
            }
            string extractedFolderName = extractedTemplatePath + model.ProjectName;
            string filePathToRead = HostingEnvironment.MapPath("~") + @"\\PreSetting";

            string projectSetting = "";
            projectSetting = filePathToRead + "\\ProjectSettings.json";
            projectSetting = System.IO.File.ReadAllText(projectSetting);
            projectSetting = projectSetting.Replace("$type$", model.ProcessTemplate);
            System.IO.File.WriteAllText(extractedFolderName + "\\ProjectSettings.json", projectSetting);

            string projectTemplate = "";
            projectTemplate = filePathToRead + "\\ProjectTemplate.json";
            projectTemplate = System.IO.File.ReadAllText(projectTemplate);
            System.IO.File.WriteAllText(extractedFolderName + "\\ProjectTemplate.json", projectTemplate);

            string teamArea = "";
            teamArea = filePathToRead + "\\TeamArea.json";
            teamArea = System.IO.File.ReadAllText(teamArea);
            System.IO.File.WriteAllText(extractedFolderName + "\\TeamArea.json", teamArea);
            AddMessage(model.id, "Team Areas");

            ExportWorkItems(appConfig);
            AddMessage(model.id, "Work Items");

            ExportRepositoryList(appConfig);
            AddMessage(model.id, "Repository and Service Endpoint");

            GetServiceEndpoints(appConfig);

            int count = GetBuildDefinitions(appConfig);
            if (count >= 1)
            {
                AddMessage(model.id, "Build Definition");
            }

            int relCount = GeneralizingGetReleaseDefinitions(appConfig);
            if (relCount >= 1)
            {
                AddMessage(model.id, "Release Definition");
            }

            StatusMessages[model.id] = "100";
            return new string[] { model.id, "" };
        }

        public Dictionary<string, int> GetWorkItemsCount(ProjectConfigurations appConfig)
        {
            string[] workItemtypes = { "Epic", "Feature", "Product Backlog Item", "Task", "Test Case", "Bug", "User Story", "Test Suite", "Test Plan" };
            GetWorkItemsCount itemsCount = new GetWorkItemsCount(appConfig.WorkItemConfig);
            Dictionary<string, int> fetchedWorkItemsCount = new Dictionary<string, int>();
            if (workItemtypes.Length > 0)
            {
                foreach (var workItem in workItemtypes)
                {
                    WorkItemFetchResponse.WorkItems WITCount = itemsCount.GetWorkItemsfromSource(workItem);
                    if (WITCount.count > 0)
                    {
                        fetchedWorkItemsCount.Add(workItem, WITCount.count);
                    }
                    else if (!string.IsNullOrEmpty(itemsCount.LastFailureMessage))
                    {
                        errorMessages.Add("Error while querying work items: " + itemsCount.LastFailureMessage);
                    }
                }
            }

            return fetchedWorkItemsCount;
        }

        public List<RequiredExtensions.ExtensionWithLink> GetInstalledExtensions(ProjectConfigurations appConfig)
        {
            try
            {
                GetListExtenison listExtenison = new GetListExtenison(appConfig.ExtensionConfig);
                List<RequiredExtensions.ExtensionWithLink> extensionList = new List<RequiredExtensions.ExtensionWithLink>();
                GetExtensions.ExtensionsList returnExtensionsList = listExtenison.GetInstalledExtensions();
                if (returnExtensionsList != null && returnExtensionsList.count > 0)
                {
                    List<GetExtensions.Value> builtInExtensions = returnExtensionsList.value.Where(x => x.flags == null).ToList();
                    List<GetExtensions.Value> trustedExtensions = returnExtensionsList.value.Where(x => x.flags != null && x.flags.ToString() == "trusted").ToList();
                    builtInExtensions.AddRange(trustedExtensions);
                    returnExtensionsList.value = builtInExtensions;

                    foreach (GetExtensions.Value data in returnExtensionsList.value)
                    {
                        RequiredExtensions.ExtensionWithLink extension = new RequiredExtensions.ExtensionWithLink();

                        extension.extensionId = data.extensionId;
                        extension.extensionName = data.extensionName;
                        extension.publisherId = data.publisherId;
                        extension.publisherName = data.publisherName;
                        extension.link = "<a href='" + string.Format("https://marketplace.visualstudio.com/items?itemName={0}.{1}", data.publisherId, data.extensionId) + "' target='_blank'><b>" + data.extensionName + "</b></a>";
                        extension.License = "<a href='" + string.Format("https://marketplace.visualstudio.com/items?itemName={0}.{1}", data.publisherId, data.extensionId) + "' target='_blank'>License Terms</a>";
                        extensionList.Add(extension);
                    }
                    RequiredExtensions.listExtension listExtension = new RequiredExtensions.listExtension();
                    if (extensionList.Count > 0)
                    {
                        listExtension.Extensions = extensionList;
                        if (!Directory.Exists(extractedTemplatePath + appConfig.ExtensionConfig.Project))
                        {
                            Directory.CreateDirectory(extractedTemplatePath + appConfig.ExtensionConfig.Project);
                        }
                        string fetchedJson = JsonConvert.SerializeObject(listExtension, Formatting.Indented);

                        System.IO.File.WriteAllText(extractedTemplatePath + appConfig.ExtensionConfig.Project + "\\Extensions.json", JsonConvert.SerializeObject(listExtension, Formatting.Indented));
                    }
                }
                else if (!string.IsNullOrEmpty(listExtenison.LastFailureMessage))
                {
                    AddMessage(appConfig.ExtensionConfig.Id.ErrorId(), "Some error occured while fetching extensions");
                }
                return extensionList;
            }
            catch (Exception ex)
            {
                logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + ex.Message + "\n" + ex.StackTrace + "\n");
            }
            return new List<RequiredExtensions.ExtensionWithLink>();
        }

        public void ExportQuries(ProjectConfigurations appConfig)
        {
            try
            {
                Queries queries = new Queries(appConfig.QueriesConfig);
                GetQueries.Queries listQueries = queries.GetQueriesWiql();
                if (listQueries.count > 0)
                {
                    foreach (var _queries in listQueries.value)
                    {
                        if (_queries.hasChildren)
                        {
                            foreach (var query in _queries.children)
                            {
                                if (!query.hasChildren)
                                {
                                    if (query.wiql != null)
                                    {
                                        query.wiql = query.wiql.Replace(appConfig.QueriesConfig.Project, "$projectId$");
                                        JObject jobj = new JObject();
                                        jobj["name"] = query.name;
                                        jobj["wiql"] = query.wiql;
                                        if (!Directory.Exists(extractedTemplatePath + appConfig.QueriesConfig.Project + "\\Dashboard\\Queries"))
                                        {
                                            Directory.CreateDirectory(extractedTemplatePath + appConfig.QueriesConfig.Project + "\\Dashboard");
                                            System.IO.File.WriteAllText(extractedTemplatePath + appConfig.QueriesConfig.Project + "\\Dashboard\\Dashboard.json", JsonConvert.SerializeObject("text", Formatting.Indented));
                                        }
                                        if (!Directory.Exists(extractedTemplatePath + appConfig.QueriesConfig.Project + "\\Dashboard\\Queries"))
                                        {
                                            Directory.CreateDirectory(extractedTemplatePath + appConfig.QueriesConfig.Project + "\\Dashboard\\Queries");
                                            System.IO.File.WriteAllText(extractedTemplatePath + appConfig.QueriesConfig.Project + "\\Dashboard\\Queries\\" + query.name + ".json", JsonConvert.SerializeObject(jobj, Formatting.Indented));
                                        }
                                        else
                                        {
                                            System.IO.File.WriteAllText(extractedTemplatePath + appConfig.QueriesConfig.Project + "\\Dashboard\\Queries\\" + query.name + ".json", JsonConvert.SerializeObject(jobj, Formatting.Indented));
                                        }
                                    }
                                }
                                else
                                {
                                    foreach (var child1 in query.children)
                                    {
                                        if (child1.wiql != null)
                                        {
                                            child1.wiql = child1.wiql.Replace(appConfig.QueriesConfig.Project, "$projectId$");
                                            JObject jobj = new JObject();
                                            jobj["name"] = child1.name;
                                            jobj["wiql"] = child1.wiql;
                                            if (!Directory.Exists(extractedTemplatePath + appConfig.QueriesConfig.Project + "\\Dashboard\\Queries"))
                                            {
                                                Directory.CreateDirectory(extractedTemplatePath + appConfig.QueriesConfig.Project + "\\Dashboard\\Queries");

                                                File.WriteAllText(extractedTemplatePath + appConfig.QueriesConfig.Project + "\\Dashboard\\Queries\\" + child1.name + ".json", JsonConvert.SerializeObject(jobj, Formatting.Indented));
                                            }
                                            else
                                            {
                                                File.WriteAllText(extractedTemplatePath + appConfig.QueriesConfig.Project + "\\Dashboard\\Queries\\" + child1.name + ".json", JsonConvert.SerializeObject(jobj, Formatting.Indented));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(queries.LastFailureMessage))
                {
                    AddMessage(appConfig.QueriesConfig.Id.ErrorId(), "Error while fetching queries");
                }
            }
            catch (Exception ex)
            {
                logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + ex.Message + "\n" + ex.StackTrace + "\n");
            }

        }

        public bool ExportTeams(Configuration con, string processTemplate, string projectID)
        {
            try
            {
                string defaultTeamID = string.Empty;
                VstsRestAPI.Extractor.ClassificationNodes nodes = new VstsRestAPI.Extractor.ClassificationNodes(con);
                TeamList _team = new TeamList();
                string ProjectPropertyVersion = System.Configuration.ConfigurationManager.AppSettings["ProjectPropertyVersion"];
                con.VersionNumber = ProjectPropertyVersion;
                con.ProjectId = projectID;
                Projects projects = new Projects(con);
                ProjectProperties.Properties projectProperties = projects.GetProjectProperties();
                if (projectProperties.count > 0)
                {
                    defaultTeamID = projectProperties.value.Where(x => x.name == "System.Microsoft.TeamFoundation.Team.Default").FirstOrDefault().value;
                }
                _team = nodes.ExportTeamList(defaultTeamID);
                if (_team.value != null)
                {
                    AddMessage(con.Id, "Teams");

                    string fetchedJson = JsonConvert.SerializeObject(_team.value, Formatting.Indented);
                    if (fetchedJson != "")
                    {
                        if (!Directory.Exists(extractedTemplatePath + con.Project + "\\Teams"))
                        {
                            Directory.CreateDirectory(extractedTemplatePath + con.Project + "\\Teams");
                        }
                        System.IO.File.WriteAllText(extractedTemplatePath + con.Project + "\\Teams\\Teams.json", fetchedJson);

                        List<string> boardTypes = new List<string>();
                        boardTypes.Add("Epics"); boardTypes.Add("Features");

                        if (processTemplate.ToLower() == "agile")
                        { boardTypes.Add("Stories"); }
                        else { boardTypes.Add("Backlog Items"); }

                        foreach (var team in _team.value)
                        {
                            List<BoardColumnResponseScrum.ColumnResponse> columnResponsesScrum = new List<BoardColumnResponseScrum.ColumnResponse>();
                            List<BoardColumnResponseAgile.ColumnResponse> columnResponsesAgile = new List<BoardColumnResponseAgile.ColumnResponse>();
                            List<ExportBoardRows.Rows> boardRows = new List<ExportBoardRows.Rows>();

                            ExportTeamSetting.Setting listTeamSetting = new ExportTeamSetting.Setting();

                            List<JObject> jObjCardFieldList = new List<JObject>();
                            List<JObject> jObjcardStyleList = new List<JObject>();
                            string teamFolderPath = extractedTemplatePath + con.Project + "\\Teams\\" + team.name;
                            if (!Directory.Exists(teamFolderPath))
                            {
                                Directory.CreateDirectory(teamFolderPath);
                            }
                            //Export Board Colums for each team
                            con.Team = team.name;

                            VstsRestAPI.Extractor.ClassificationNodes teamNodes = new VstsRestAPI.Extractor.ClassificationNodes(con);
                            foreach (var boardType in boardTypes)
                            {
                                var response = teamNodes.ExportBoardColums(boardType);
                                if (response.IsSuccessStatusCode && response.StatusCode == System.Net.HttpStatusCode.OK)
                                {
                                    if (processTemplate.ToLower() == "scrum")
                                    {
                                        string res = response.Content.ReadAsStringAsync().Result;
                                        BoardColumnResponseScrum.ColumnResponse scrumColumns = JsonConvert.DeserializeObject<BoardColumnResponseScrum.ColumnResponse>(res);
                                        scrumColumns.BoardName = boardType;
                                        columnResponsesScrum.Add(scrumColumns);
                                    }
                                    else if (processTemplate.ToLower() == "agile")
                                    {
                                        string res = response.Content.ReadAsStringAsync().Result;
                                        BoardColumnResponseAgile.ColumnResponse agileColumns = JsonConvert.DeserializeObject<BoardColumnResponseAgile.ColumnResponse>(res);
                                        agileColumns.BoardName = boardType;
                                        columnResponsesAgile.Add(agileColumns);
                                    }
                                    AddMessage(con.Id, "Board Columns");
                                    Thread.Sleep(2000);
                                }
                                else
                                {
                                    var errorMessage = response.Content.ReadAsStringAsync();
                                    string error = Utility.GeterroMessage(errorMessage.Result.ToString());
                                    teamNodes.LastFailureMessage = error;
                                    AddMessage(con.Id.ErrorId(), "Error occured while exporting Board Columns: " + teamNodes.LastFailureMessage);
                                }

                                //Export board rows for each team
                                ExportBoardRows.Rows rows = teamNodes.ExportBoardRows(boardType);
                                if (rows.value != null && rows.value.Count > 0)
                                {
                                    rows.BoardName = boardType;
                                    boardRows.Add(rows);
                                    AddMessage(con.Id, "Board Rows");
                                    Thread.Sleep(2000);
                                }
                                else if (!string.IsNullOrEmpty(teamNodes.LastFailureMessage))
                                {
                                    AddMessage(con.Id.ErrorId(), "Error occured while exporting Board Rows: " + teamNodes.LastFailureMessage);
                                }


                                //Export Card Fields for each team
                                var cardFieldResponse = teamNodes.ExportCardFields(boardType);
                                if (cardFieldResponse.IsSuccessStatusCode && cardFieldResponse.StatusCode == System.Net.HttpStatusCode.OK)
                                {
                                    string res = cardFieldResponse.Content.ReadAsStringAsync().Result;
                                    JObject jObj = JsonConvert.DeserializeObject<JObject>(res);
                                    jObj["BoardName"] = boardType;
                                    jObjCardFieldList.Add(jObj);
                                    AddMessage(con.Id, "Card fields Definition");

                                }
                                else
                                {
                                    var errorMessage = cardFieldResponse.Content.ReadAsStringAsync();
                                    string error = Utility.GeterroMessage(errorMessage.Result.ToString());
                                    teamNodes.LastFailureMessage = error;
                                    AddMessage(con.Id.ErrorId(), "Error occured while exporting Card Fields: " + teamNodes.LastFailureMessage);
                                }

                                //// Export card styles for each team
                                var cardStyleResponse = teamNodes.ExportCardStyle(boardType);
                                if (cardStyleResponse.IsSuccessStatusCode && cardStyleResponse.StatusCode == System.Net.HttpStatusCode.OK)
                                {
                                    string res = cardStyleResponse.Content.ReadAsStringAsync().Result;
                                    res = res.Replace(con.Project, "$ProjectName$");
                                    JObject jObj = JsonConvert.DeserializeObject<JObject>(res);
                                    jObj["BoardName"] = boardType;
                                    var style = jObj;
                                    style["url"] = "";
                                    style["_links"] = "{}";
                                    var tagStyle = style["rules"]["tagStyle"];
                                    if (tagStyle == null)
                                    {
                                        style["rules"]["tagStyle"] = new JArray();
                                    }
                                    jObjcardStyleList.Add(jObj);
                                    AddMessage(con.Id, "Card style");

                                }
                                else
                                {
                                    var errorMessage = cardStyleResponse.Content.ReadAsStringAsync();
                                    string error = Utility.GeterroMessage(errorMessage.Result.ToString());
                                    teamNodes.LastFailureMessage = error;
                                    AddMessage(con.Id.ErrorId(), "Error occured while exporting Card Styles: " + teamNodes.LastFailureMessage);
                                }
                            }
                            //Export Team Setting for each team
                            ExportTeamSetting.Setting teamSetting = teamNodes.ExportTeamSetting();
                            if (teamSetting.backlogVisibilities != null)
                            {
                                listTeamSetting = teamSetting;
                                AddMessage(con.Id, "Team Settings Definition");
                            }
                            else if (!string.IsNullOrEmpty(teamNodes.LastFailureMessage))
                            {
                                AddMessage(con.Id.ErrorId(), "Error occured while exporting Team Setting: " + teamNodes.LastFailureMessage);
                            }

                            if (columnResponsesAgile.Count > 0)
                            {
                                System.IO.File.WriteAllText(teamFolderPath + "\\BoardColumns.json", JsonConvert.SerializeObject(columnResponsesAgile, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
                            }
                            if (columnResponsesScrum.Count > 0)
                            {
                                System.IO.File.WriteAllText(teamFolderPath + "\\BoardColumns.json", JsonConvert.SerializeObject(columnResponsesScrum, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
                            }
                            if (boardRows.Count > 0)
                            {
                                System.IO.File.WriteAllText(teamFolderPath + "\\BoardRows.json", JsonConvert.SerializeObject(boardRows, Formatting.Indented));
                            }
                            if (!string.IsNullOrEmpty(listTeamSetting.bugsBehavior))
                            {
                                System.IO.File.WriteAllText(teamFolderPath + "\\TeamSetting.json", JsonConvert.SerializeObject(listTeamSetting, Formatting.Indented));
                            }
                            if (jObjCardFieldList.Count > 0)
                            {
                                System.IO.File.WriteAllText(teamFolderPath + "\\CardFields.json", JsonConvert.SerializeObject(jObjCardFieldList, Formatting.Indented));
                            }
                            if (jObjcardStyleList.Count > 0)
                            {
                                System.IO.File.WriteAllText(teamFolderPath + "\\CardStyles.json", JsonConvert.SerializeObject(jObjcardStyleList, Formatting.Indented));
                            }
                        }

                        return true;
                    }
                    else if (!string.IsNullOrEmpty(nodes.LastFailureMessage))
                    {
                        AddMessage(con.Id.ErrorId(), nodes.LastFailureMessage);
                        string error = nodes.LastFailureMessage;
                        return false;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    AddMessage(con.Id.ErrorId(), nodes.LastFailureMessage);
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + ex.Message + "\n" + ex.StackTrace + "\n");
            }
            return false;
        }

        public bool ExportIterations(ProjectConfigurations appConfig)
        {
            try
            {
                ClassificationNodes nodes = new VstsRestAPI.Extractor.ClassificationNodes(appConfig.BoardConfig);
                ExportedIterations.Iterations viewModel = nodes.ExportIterationsToSave();
                string fetchedJson = JsonConvert.SerializeObject(viewModel, Formatting.Indented);
                if (fetchedJson != "")
                {
                    if (!Directory.Exists(extractedTemplatePath + appConfig.BoardConfig.Project))
                    {
                        Directory.CreateDirectory(extractedTemplatePath + appConfig.BoardConfig.Project);
                    }
                    System.IO.File.WriteAllText(extractedTemplatePath + appConfig.BoardConfig.Project + "\\Iterations.json", fetchedJson);
                    return true;
                }
                else
                {
                    string error = nodes.LastFailureMessage;
                    AddMessage(appConfig.BoardConfig.Id.ErrorId(), error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + ex.Message + "\n" + ex.StackTrace + "\n");
            }
            return false;
        }

        public void ExportWorkItems(ProjectConfigurations appConfig)
        {
            string[] workItemtypes = { "Epic", "Feature", "Product Backlog Item", "Task", "Test Case", "Bug", "User Story", "Test Suite", "Test Plan" };
            if (!Directory.Exists(extractedTemplatePath + appConfig.WorkItemConfig.Project))
            {
                Directory.CreateDirectory(extractedTemplatePath + appConfig.WorkItemConfig.Project);
            }

            if (workItemtypes.Length > 0)
            {
                foreach (var WIT in workItemtypes)
                {
                    GetWorkItemsCount WorkitemsCount = new GetWorkItemsCount(appConfig.WorkItemConfig);
                    WorkItemFetchResponse.WorkItems fetchedWorkItem = WorkitemsCount.GetWorkItemsfromSource(WIT);
                    string workItemJson = JsonConvert.SerializeObject(fetchedWorkItem, Formatting.Indented);
                    if (fetchedWorkItem.count > 0)
                    {
                        string item = WIT;
                        if (!Directory.Exists(extractedTemplatePath + appConfig.WorkItemConfig.Project + "\\WorkItems"))
                        {
                            Directory.CreateDirectory(extractedTemplatePath + appConfig.WorkItemConfig.Project + "\\WorkItems");
                        }
                        System.IO.File.WriteAllText(extractedTemplatePath + appConfig.WorkItemConfig.Project + "\\WorkItems\\" + item + ".json", workItemJson);
                    }
                    else if (!string.IsNullOrEmpty(WorkitemsCount.LastFailureMessage))
                    {
                        AddMessage(appConfig.WorkItemConfig.Id.ErrorId(), WorkitemsCount.LastFailureMessage);
                    }
                }
            }
        }

        public void ExportRepositoryList(ProjectConfigurations appConfig)
        {
            BuildandReleaseDefs repolist = new BuildandReleaseDefs(appConfig.RepoConfig);
            RepositoryList.Repository repos = repolist.GetRepoList();
            if (repos.count > 0)
            {
                foreach (var repo in repos.value)
                {
                    string preSettingPath = HostingEnvironment.MapPath("~") + @"PreSetting";
                    string templateFolderPath = extractedTemplatePath + appConfig.RepoConfig.Project;
                    string host = appConfig.RepoConfig.UriString + appConfig.RepoConfig.Project;
                    string sourceCodeJson = System.IO.File.ReadAllText(preSettingPath + "\\ImportSourceCode.json");
                    sourceCodeJson = sourceCodeJson.Replace("$Host$", host).Replace("$Repo$", repo.name);
                    string endPointJson = System.IO.File.ReadAllText(preSettingPath + "\\ServiceEndPoint.json");
                    endPointJson = endPointJson.Replace("$Host$", host).Replace("$Repo$", repo.name);
                    if (!Directory.Exists(templateFolderPath + "\\ImportSourceCode"))
                    {
                        Directory.CreateDirectory(templateFolderPath + "\\ImportSourceCode");
                        System.IO.File.WriteAllText(templateFolderPath + "\\ImportSourceCode\\" + repo.name + ".json", sourceCodeJson);
                    }
                    else
                    {
                        System.IO.File.WriteAllText(templateFolderPath + "\\ImportSourceCode\\" + repo.name + ".json", sourceCodeJson);
                    }
                    if (!Directory.Exists(templateFolderPath + "\\ServiceEndpoints"))
                    {
                        Directory.CreateDirectory(templateFolderPath + "\\ServiceEndpoints");
                        System.IO.File.WriteAllText(templateFolderPath + "\\ServiceEndpoints\\" + repo.name + "-code.json", endPointJson);
                    }
                    else
                    {
                        System.IO.File.WriteAllText(templateFolderPath + "\\ServiceEndpoints\\" + repo.name + "-code.json", endPointJson);
                    }
                }
            }
        }

        // Get the Build definitions to write into file
        public int GetBuildDefinitions(ProjectConfigurations appConfig)
        {
            try
            {
                BuildandReleaseDefs buildandReleaseDefs = new BuildandReleaseDefs(appConfig.BuildDefinitionConfig);
                List<JObject> builds = buildandReleaseDefs.ExportBuildDefinitions();
                BuildandReleaseDefs repoDefs = new BuildandReleaseDefs(appConfig.RepoConfig);
                RepositoryList.Repository repo = repoDefs.GetRepoList();
                string esr = JsonConvert.SerializeObject(builds);
                if (builds.Count > 0)
                {
                    int count = 1;
                    //creating ImportCode Json file
                    string templatePath = extractedTemplatePath + appConfig.BuildDefinitionConfig.Project;
                    foreach (JObject def in builds)
                    {
                        string repoID = "";
                        var buildName = def["name"];
                        var repoName = def["repository"]["name"];
                        foreach (var re in repo.value)
                        {
                            if (re.name == repoName.ToString())
                            {
                                repoID = re.id;
                            }
                        }
                        def["authoredBy"] = "{}";
                        def["project"] = "{}";
                        def["url"] = "";
                        def["uri"] = "";
                        def["id"] = "";
                        def["queue"]["id"] = "";
                        def["queue"]["url"] = "";
                        def["queue"]["_links"] = "{}";
                        def["queue"]["pool"]["id"] = "";
                        def["_links"] = "{}";
                        def["createdDate"] = "";
                        var yamalfilename = def["process"]["yamlFilename"];
                        if (yamalfilename != null)
                        {
                            def["triggers"] = new JArray();
                            var type = def["repository"]["type"];
                            if (type.ToString().ToLower() == "github")
                            {
                                //def["repository"]["type"] = "Git";
                                def["repository"]["properties"]["fullName"] = "repository";
                                def["repository"]["properties"]["connectedServiceId"] = "$GitHub$";
                                def["repository"]["name"] = "repository";
                            }
                            //def["repository"]["id"] = "$" + def["repository"]["name"] + "$";

                            var ymlRepoUrl = def["repository"]["url"].ToString();
                            if (ymlRepoUrl != "")
                            {
                                string endPointString = System.IO.File.ReadAllText(HostingEnvironment.MapPath("~") + @"PreSetting\\GitHubEndPoint.json");
                                endPointString = endPointString.Replace("$GitHubURL$", ymlRepoUrl);
                                Guid g = Guid.NewGuid();
                                string randStr = g.ToString().Substring(0, 8);
                                if (!Directory.Exists(extractedTemplatePath + appConfig.BuildDefinitionConfig.Project + "\\ServiceEndpoints"))
                                {
                                    Directory.CreateDirectory(extractedTemplatePath + appConfig.BuildDefinitionConfig.Project + "\\ServiceEndpoints");
                                    System.IO.File.WriteAllText(extractedTemplatePath + appConfig.BuildDefinitionConfig.Project + "\\ServiceEndpoints\\GitHub-" + randStr + "-EndPoint.json", endPointString);
                                }
                                else
                                {
                                    System.IO.File.WriteAllText(extractedTemplatePath + appConfig.BuildDefinitionConfig.Project + "\\ServiceEndpoints\\GitHub-" + randStr + "-EndPoint.json", endPointString);
                                }
                            }
                            string[] splitYmlRepoUrl = ymlRepoUrl.Split('/');
                            if (splitYmlRepoUrl.Length > 0)
                            {
                                splitYmlRepoUrl[2] = "$Organization$@dev.azure.com";
                                splitYmlRepoUrl[3] = "$Organization$";
                                splitYmlRepoUrl[4] = "$ProjectName$";
                                ymlRepoUrl = string.Join("/", splitYmlRepoUrl);
                                def["repository"]["url"] = ymlRepoUrl;
                            }

                            var queueHref = def["queue"]["_links"]["self"]["href"].ToString();
                            string[] splitQhref = queueHref.Split('/');
                            if (splitQhref.Length > 0)
                            {
                                splitQhref[3] = "$Organization$";
                                splitQhref[splitQhref.Length - 1] = "$" + def["queue"]["name"].ToString() + "$";
                                def["queue"]["_links"]["self"]["href"] = string.Join("/", splitQhref);
                            }
                            def["queue"]["id"] = "$" + def["queue"]["name"] + "$";
                            def["queue"]["url"] = string.Join("/", splitQhref);
                            //AddMessage(con.Id.ErrorId(), "Not supporting yml pipelines");
                            //return count = 0;
                        }
                        else
                        {

                            def["queue"]["id"] = "";
                            def["queue"]["url"] = "";
                            def["queue"]["_links"] = "{}";
                            def["queue"]["pool"]["id"] = "";
                            def["_links"] = "{}";
                            def["createdDate"] = "";

                            var process = def["process"];
                            if (process != null)
                            {
                                var phases = process["phases"];
                                if (phases != null)
                                {
                                    foreach (var phase in phases)
                                    {
                                        phase["target"]["queue"] = "{}";
                                        var steps = phase["steps"];
                                        if (steps != null)
                                        {
                                            foreach (var step in steps)
                                            {
                                                string keyConfig = System.IO.File.ReadAllText(HostingEnvironment.MapPath("~") + @"\\Templates\EndpointKeyConfig.json");
                                                KeyConfig.Keys keyC = new KeyConfig.Keys();
                                                keyC = JsonConvert.DeserializeObject<KeyConfig.Keys>(keyConfig);
                                                foreach (var key in keyC.keys)
                                                {
                                                    string keyVal = step[key] != null ? step[key].ToString() : "";
                                                    if (!string.IsNullOrEmpty(keyVal))
                                                    {
                                                        step[key] = "";
                                                    }
                                                }
                                                foreach (var key in keyC.keys)
                                                {
                                                    string keyVal = step["inputs"][key] != null ? step["inputs"][key].ToString() : "";
                                                    if (!string.IsNullOrEmpty(keyVal))
                                                    {
                                                        step["inputs"][key] = "";
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            var type = def["repository"]["type"];
                            if (type.ToString().ToLower() == "github")
                            {
                                def["repository"]["type"] = "Git";
                                def["repository"]["properties"]["fullName"] = "repository";
                                def["repository"]["properties"]["connectedServiceId"] = "$GitHub$";
                                def["repository"]["name"] = "repository";
                                string url = def["repository"]["url"].ToString();
                                if (url != "")
                                {
                                    string endPointString = System.IO.File.ReadAllText(HostingEnvironment.MapPath("~") + @"PreSetting\\GitHubEndPoint.json");
                                    endPointString = endPointString.Replace("$GitHubURL$", url);
                                    Guid g = Guid.NewGuid();
                                    string randStr = g.ToString().Substring(0, 8);
                                    if (!Directory.Exists(extractedTemplatePath + appConfig.RepoConfig.Project + "\\ServiceEndpoints"))
                                    {
                                        Directory.CreateDirectory(extractedTemplatePath + appConfig.RepoConfig.Project + "\\ServiceEndpoints");
                                        System.IO.File.WriteAllText(extractedTemplatePath + appConfig.RepoConfig.Project + "\\ServiceEndpoints\\GitHub-" + randStr + "-EndPoint.json", endPointString);
                                    }
                                    else
                                    {
                                        System.IO.File.WriteAllText(extractedTemplatePath + appConfig.RepoConfig.Project + "\\ServiceEndpoints\\GitHub-" + randStr + "-EndPoint.json", endPointString);
                                    }
                                }
                            }
                            else if (type.ToString().ToLower() == "git")
                            {
                                string url = def["repository"]["url"].ToString();
                                string endPointString = System.IO.File.ReadAllText(HostingEnvironment.MapPath("~") + @"PreSetting\\GitHubEndPoint.json");
                                endPointString = endPointString.Replace("$GitHubURL$", url);
                                Guid g = Guid.NewGuid();
                                string randStr = g.ToString().Substring(0, 8);
                                if (!Directory.Exists(extractedTemplatePath + appConfig.RepoConfig.Project + "\\ServiceEndpoints"))
                                {
                                    Directory.CreateDirectory(extractedTemplatePath + appConfig.RepoConfig.Project + "\\ServiceEndpoints");
                                    System.IO.File.WriteAllText(extractedTemplatePath + appConfig.RepoConfig.Project + "\\ServiceEndpoints\\GitHub-" + randStr + "-EndPoint.json", endPointString);
                                }
                                else
                                {
                                    System.IO.File.WriteAllText(extractedTemplatePath + appConfig.RepoConfig.Project + "\\ServiceEndpoints\\GitHub-" + randStr + "-EndPoint.json", endPointString);
                                }
                                def["repository"]["properties"]["connectedServiceId"] = "$GitHub$";
                            }
                            else
                            {
                                def["repository"]["id"] = "$" + repoName + "$";
                                def["repository"]["url"] = "";
                                def["repository"]["properties"]["connectedServiceId"] = "";
                            }
                            var input = def["processParameters"]["inputs"];
                            if (input != null)
                            {
                                if (input.HasValues)
                                {
                                    foreach (var i in input)
                                    {
                                        i["defaultValue"] = "";

                                    }
                                }
                            }
                            var build = def["build"];
                            if (build != null)
                            {
                                if (build.HasValues)
                                {
                                    foreach (var b in build)
                                    {
                                        b["inputs"]["serverEndpoint"] = "";
                                    }
                                }
                            }
                        }
                        if (!Directory.Exists(templatePath + "\\BuildDefinitions"))
                        {
                            Directory.CreateDirectory(templatePath + "\\BuildDefinitions");
                            System.IO.File.WriteAllText(templatePath + "\\BuildDefinitions\\" + buildName + ".json", JsonConvert.SerializeObject(def, Formatting.Indented));
                        }
                        else
                        {
                            System.IO.File.WriteAllText(templatePath + "\\BuildDefinitions\\" + buildName + ".json", JsonConvert.SerializeObject(def, Formatting.Indented));
                        }
                        count = count + 1;
                    }
                    return count;
                }
            }
            catch (Exception ex)
            {
                logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + ex.Message + "\n" + ex.StackTrace + "\n");
            }
            return 0;
        }

        // Generalizing the release definition method to make it work for All kind of Release definition
        public int GeneralizingGetReleaseDefinitions(ProjectConfigurations appConfig)
        {
            try
            {
                BuildandReleaseDefs releaseDefs = new BuildandReleaseDefs(appConfig.ReleaseDefinitionConfig);
                List<JObject> releases = releaseDefs.GetReleaseDefs();
                BuildandReleaseDefs agent = new BuildandReleaseDefs(appConfig.AgentQueueConfig);

                Dictionary<string, int> queue = agent.GetQueues();
                string templatePath = extractedTemplatePath + appConfig.ReleaseDefinitionConfig.Project;
                int releasecount = 1;
                if (releases.Count > 0)
                {
                    foreach (JObject rel in releases)
                    {
                        var name = rel["name"];
                        rel["id"] = "";
                        rel["url"] = "";
                        rel["_links"] = "{}";
                        rel["createdBy"] = "{}";
                        rel["createdOn"] = "";
                        rel["modifiedBy"] = "{}";
                        rel["modifiedOn"] = "";
                        var env = rel["environments"];
                        foreach (var e in env)
                        {
                            e["badgeUrl"] = "";
                            var owner = e["owner"];
                            owner["id"] = "$OwnerId$";
                            owner["displayName"] = "$OwnerDisplayName$";
                            owner["uniqueName"] = "$OwnerUniqueName$";
                            owner["url"] = "";
                            owner["_links"] = "{}";
                            owner["imageUrl"] = "";
                            owner["descriptor"] = "";

                            var deployPhases = e["deployPhases"];
                            if (deployPhases.HasValues)
                            {
                                foreach (var dep in deployPhases)
                                {

                                    var deploymentInput = dep["deploymentInput"];
                                    var queueID = deploymentInput["queueId"];
                                    string queueName = "";
                                    if (queue != null)
                                    {
                                        if (queue.Count > 0)
                                        {
                                            var agenetName = queue.Where(x => x.Value.ToString() == queueID.ToString()).FirstOrDefault();
                                            if (agenetName.Key != null)
                                            {
                                                queueName = agenetName.Key.ToString();
                                            }
                                            else
                                            {
                                                queueName = "";
                                            }
                                        }
                                    }
                                    if (queueName != "")
                                    {
                                        deploymentInput["queueId"] = "$" + queueName + "$";
                                    }
                                    else
                                    {
                                        deploymentInput["queueId"] = "";
                                    }

                                    var workflow = dep["workflowTasks"];
                                    if (workflow.HasValues)
                                    {
                                        foreach (var flow in workflow)
                                        {
                                            var input = flow["inputs"];
                                            string keyConfig = System.IO.File.ReadAllText(HostingEnvironment.MapPath("~") + @"\\Templates\EndpointKeyConfig.json");
                                            KeyConfig.Keys keyC = new KeyConfig.Keys();
                                            keyC = JsonConvert.DeserializeObject<KeyConfig.Keys>(keyConfig);
                                            foreach (var key in keyC.keys)
                                            {
                                                string keyVal = input[key] != null ? input[key].ToString() : "";
                                                if (!string.IsNullOrEmpty(keyVal))
                                                {
                                                    input[key] = "";
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        var artifact = rel["artifacts"];
                        if (artifact.HasValues)
                        {
                            foreach (var art in artifact)
                            {
                                string buildName = art["definitionReference"]["definition"]["name"].ToString();

                                art["sourceId"] = "$ProjectId$:" + "$" + buildName + "-id$";
                                art["definitionReference"]["definition"]["id"] = "$" + buildName + "-id$";
                                art["definitionReference"]["project"]["id"] = "$ProjectId$";
                                art["definitionReference"]["project"]["name"] = "$ProjectName$";
                                art["definitionReference"]["artifactSourceDefinitionUrl"] = "{}";
                            }
                        }
                        if (!(Directory.Exists(templatePath + "\\ReleaseDefinitions")))
                        {
                            Directory.CreateDirectory(templatePath + "\\ReleaseDefinitions");
                            System.IO.File.WriteAllText(templatePath + "\\ReleaseDefinitions\\" + name + ".json", JsonConvert.SerializeObject(rel, Formatting.Indented));
                        }
                        else
                        {
                            System.IO.File.WriteAllText(templatePath + "\\ReleaseDefinitions\\" + name + ".json", JsonConvert.SerializeObject(rel, Formatting.Indented));
                        }
                        releasecount++;
                    }
                }
                return releasecount;
            }
            catch (Exception ex)
            {
                logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + ex.Message + "\n" + ex.StackTrace + "\n");
                AddMessage(appConfig.ReleaseDefinitionConfig.Id.ErrorId(), ex.Message + Environment.NewLine + ex.StackTrace);
            }
            return 0;
        }

        public void GetServiceEndpoints(ProjectConfigurations appConfig)
        {
            try
            {
                ServiceEndPoint serviceEndPoint = new ServiceEndPoint(appConfig.EndpointConfig);
                Parameters.ServiceEndPoint getServiceEndPoint = serviceEndPoint.GetServiceEndPoints();
                if (getServiceEndPoint.count > 0)
                {
                    foreach (Parameters.Value endpoint in getServiceEndPoint.value)
                    {
                        switch (endpoint.authorization.scheme)
                        {
                            case "OAuth":
                            case "InstallationToken":
                                switch (endpoint.type)
                                {
                                    case "github":
                                    case "GitHub":
                                        if (endpoint.authorization.parameters == null)
                                        {
                                            endpoint.authorization.parameters = new Parameters.Parameters
                                            {
                                                AccessToken = "AccessToken"
                                            };
                                        }
                                        else
                                        {
                                            endpoint.authorization.parameters.AccessToken = endpoint.authorization.parameters.AccessToken ?? "AccessToken";
                                        }
                                        break;
                                }
                                break;
                            case "UsernamePassword":
                                endpoint.authorization.parameters.username = endpoint.authorization.parameters.username ?? "username";
                                endpoint.authorization.parameters.password = endpoint.authorization.parameters.password ?? "password";
                                break;
                            case "ManagedServiceIdentity":
                                if (endpoint.authorization.parameters == null)
                                {
                                    endpoint.authorization.parameters = new Parameters.Parameters
                                    {
                                        tenantId = Guid.NewGuid().ToString()
                                    };
                                }
                                else
                                {
                                    endpoint.authorization.parameters.tenantId = endpoint.authorization.parameters.tenantId ?? Guid.NewGuid().ToString();
                                }
                                break;
                            case "ServicePrincipal":
                                switch (endpoint.type)
                                {
                                    case "devCenter":
                                        endpoint.authorization.parameters.servicePrincipalKey = endpoint.authorization.parameters.servicePrincipalKey ?? "P2ssw0rd@123";
                                        break;
                                    case "azurerm":
                                        endpoint.authorization.parameters.url = null;
                                        endpoint.authorization.parameters.servicePrincipalId = endpoint.authorization.parameters.servicePrincipalId ?? Guid.NewGuid().ToString();
                                        endpoint.authorization.parameters.authenticationType = endpoint.authorization.parameters.authenticationType ?? "spnKey";
                                        endpoint.authorization.parameters.tenantId = endpoint.authorization.parameters.tenantId ?? Guid.NewGuid().ToString();
                                        endpoint.authorization.parameters.servicePrincipalKey = endpoint.authorization.parameters.servicePrincipalKey ?? "spnKey";
                                        break;
                                }
                                break;
                            case "Certificate":
                                switch (endpoint.type)
                                {
                                    case "dockerhost":
                                        if (endpoint.authorization.parameters == null)
                                        {
                                            endpoint.authorization.parameters = new Parameters.Parameters();
                                            endpoint.authorization.parameters.cacert = endpoint.authorization.parameters.cacert ?? "cacert";
                                            endpoint.authorization.parameters.cert = endpoint.authorization.parameters.cert ?? "cert";
                                            endpoint.authorization.parameters.key = endpoint.authorization.parameters.key ?? "key";
                                        }
                                        else
                                        {
                                            endpoint.authorization.parameters.cacert = endpoint.authorization.parameters.cacert ?? "cacert";
                                            endpoint.authorization.parameters.cert = endpoint.authorization.parameters.cert ?? "cert";
                                            endpoint.authorization.parameters.key = endpoint.authorization.parameters.key ?? "key";
                                        }
                                        break;

                                    case "azure":
                                        if (endpoint.authorization.parameters == null)
                                        {
                                            endpoint.authorization.parameters = new Parameters.Parameters
                                            {
                                                certificate = "certificate"
                                            };
                                        }
                                        else
                                        {
                                            endpoint.authorization.parameters.certificate = endpoint.authorization.parameters.certificate ?? "certificate";
                                        }
                                        break;
                                }
                                break;
                            case "Token":
                                if (endpoint.authorization.parameters == null)
                                {
                                    endpoint.authorization.parameters = new Parameters.Parameters
                                    {
                                        apitoken = "apitoken"
                                    };
                                }
                                else
                                {
                                    endpoint.authorization.parameters.apitoken = endpoint.authorization.parameters.apitoken ?? "apitoken";
                                }
                                break;
                            case "None":
                                switch (endpoint.type)
                                {
                                    case "AzureServiceBus":
                                        if (endpoint.authorization.parameters == null)
                                        {
                                            endpoint.authorization.parameters = new Parameters.Parameters
                                            {
                                                serviceBusConnectionString = "connectionstring"
                                            };
                                        }
                                        else
                                        {
                                            endpoint.authorization.parameters.serviceBusConnectionString = endpoint.authorization.parameters.serviceBusConnectionString ?? "connectionstring";
                                        }
                                        break;
                                    case "externalnugetfeed":
                                        if (endpoint.authorization.parameters == null)
                                        {
                                            endpoint.authorization.parameters = new Parameters.Parameters
                                            {
                                                nugetkey = "nugetkey"
                                            };
                                        }
                                        else
                                        {
                                            endpoint.authorization.parameters.nugetkey = endpoint.authorization.parameters.nugetkey ?? "nugetkey";
                                        }
                                        break;
                                }
                                break;

                        }
                        string endpointString = JsonConvert.SerializeObject(endpoint);
                        if (!Directory.Exists(extractedTemplatePath + appConfig.EndpointConfig.Project + "\\ServiceEndpoints"))
                        {
                            Directory.CreateDirectory(extractedTemplatePath + appConfig.EndpointConfig.Project + "\\ServiceEndpoints");
                            System.IO.File.WriteAllText(extractedTemplatePath + appConfig.EndpointConfig.Project + "\\ServiceEndpoints\\", JsonConvert.SerializeObject(endpoint, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
                        }
                        else
                        {
                            System.IO.File.WriteAllText(extractedTemplatePath + appConfig.EndpointConfig.Project + "\\ServiceEndpoints\\" + endpoint.name + ".json", JsonConvert.SerializeObject(endpoint, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(serviceEndPoint.LastFailureMessage))
                {
                    AddMessage(appConfig.EndpointConfig.Id.ErrorId(), "Error occured while fetchin service endpoints");
                }
            }
            catch (Exception ex)
            {
                logger.Info(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + ex.Message + "\n" + ex.StackTrace + "\n");
            }
        }

        #endregion END GENERATE ARTIFACTS
    }
}