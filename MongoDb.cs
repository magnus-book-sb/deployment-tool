using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace DeploymentTool
{
	public enum BuildStatus { Passed, Failed, Unknown };

	public class BuildRecord
	{
		[BsonId]
		public ObjectId Id { get; set; }
		[BsonElement("build")]
		public string BuildNumber { get; set; }

		/// Linux, Win64, PS4, XboxOne
		[BsonElement("platform")]
		public string Platform { get; set; }

		/// Editor, Development, Test, Shipping
		[BsonElement("solution")]
		public string Solution { get; set; }

		/// Server, Client
		[BsonElement("gametype")]
		public string GameType { get; set; }

		// "\\SEH-DLAN-01\E-Builds\ue4\trunk\Win64\Development"
		[BsonElement("path")]
		public string Path { get; set; }

		[BsonElement("downloadedpath")]
		public string DownloadedPath { get; set; }

		[BsonElement("datetime")]
		public DateTime Timestamp { get; set; }

		[BsonElement("automatedtest")]
		public string Status { get; set; }

		public BuildRecord(string BuildNumber, string Platform, string Solution, string GameType, string Path, string DownloadedPath, DateTime Timestamp, string Status)
		{
			this.BuildNumber = BuildNumber;
			this.Platform = Platform;
			this.Solution = Solution;
			this.GameType = GameType;
			this.Path = Path;
			this.DownloadedPath = DownloadedPath;
			this.Timestamp = Timestamp;
			this.Status = Status;
		}
	}

	public class MongoDb
	{
		private IMongoClient Client;
		private string ConnectionString = "mongodb+srv://GauntletUser:SzH2EJgDjLGz7hlM@shootergamegauntlettest-pt7p6.mongodb.net/test?retryWrites=true&w=majority";
		private string DataBaseName = "ShooterGameGauntletTest";

		public IMongoDatabase Database { get; }

		public MongoDb()
		{
            Client = new MongoClient(ConnectionString);
            Database = Client.GetDatabase(DataBaseName);
		}

		public List<BuildRecord> SelectAvailableBuilds(FilterDefinition<BuildRecord> Filter)
		{
			var Collection = Database.GetCollection<BuildRecord>("BuildRecord");
			var Records = Collection.Find(Filter);
			return Records.ToList();
		}
		public List<BuildRecord> SelectAvailableBuildsSortedAndLimited(FilterDefinition<BuildRecord> Filter, SortDefinition<BuildRecord> Sort, int? FirstBuildsCount)
		{
			var Collection = Database.GetCollection<BuildRecord>("BuildRecord");
			var Records = Collection.Find(Filter).Sort(Sort).Limit(FirstBuildsCount);
			return Records.ToList();
		}

		public List<BuildRecord> GetAvailableBuilds(PlatformType Platform, SolutionType Solution, RoleType Role, int? FirstBuildsCount = null)
		{
			var FilterBuilder = new FilterDefinitionBuilder<BuildRecord>();
			var Filter = FilterBuilder.Eq("Platform", Platform.ToString()) &
						 FilterBuilder.Eq("Solution", Solution.ToString()) &
						 FilterBuilder.Eq("GameType", Role.ToString());

			var SortObj = Builders<BuildRecord>.Sort.Descending("datetime");
			var AvailableBuilds = SelectAvailableBuildsSortedAndLimited(Filter, SortObj, FirstBuildsCount);

			return AvailableBuilds;
		}

		public List<BuildRecord> GetAvailableBuilds(PlatformType Platform, RoleType Role)
		{
			var FilterBuilder = new FilterDefinitionBuilder<BuildRecord>();
			var Filter = FilterBuilder.Eq("Platform", Platform.ToString()) &
						 FilterBuilder.Eq("GameType", Role.ToString());

			var AvailableBuilds = SelectAvailableBuilds(Filter);

			AvailableBuilds.Sort((x, y) => DateTime.Compare(y.Timestamp, x.Timestamp));

			return AvailableBuilds;
		}

	}
}
