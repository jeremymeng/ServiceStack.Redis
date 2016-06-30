using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using NUnit.Framework;
using ServiceStack.Text;

namespace ServiceStack.Redis.Tests
{
	[TestFixture, Category("Integration")]
	public class RedisManagerPoolTests
	{
		readonly string[] hosts = new[] {
			"readwrite1", "readwrite2:6000", "192.168.0.1", "localhost"
		};

		readonly string[] testReadOnlyHosts = new[] {
			"read1", "read2:7000", "127.0.0.1"
		};

		private string firstReadWriteHost;
		private string firstReadOnlyHost;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            RedisConfig.VerifyMasterConnections = false;
        }

        [TestFixtureTearDown]
        public void TestFixtureTearDown()
        {
            RedisConfig.VerifyMasterConnections = true;
        }

		[SetUp]
		public void OnBeforeEachTest()
		{
			firstReadWriteHost = hosts[0];
			firstReadOnlyHost = testReadOnlyHosts[0];
		}

        public RedisManagerPool CreateManager()
		{
            return new RedisManagerPool(hosts);
		}

        [Test]
        public void Can_change_db_for_client()
        {
            using (var db1 = new RedisManagerPool(TestConfig.SingleHost + "?db=1"))
            using (var db2 = new RedisManagerPool(TestConfig.SingleHost + "?db=2"))
            {
                var val = Environment.TickCount;
                var key = "test" + val;
                var db1c = db1.GetClient();
                var db2c = db2.GetClient();
                try
                {
                    db1c.Set(key, val);
                    Assert.That(db2c.Get<int>(key), Is.EqualTo(0));
                    Assert.That(db1c.Get<int>(key), Is.EqualTo(val));
                }
                finally
                {
                    db1c.Remove(key);
                }
            }
        }

		[Test]
		public void Can_get_ReadWrite_client()
		{
            using (var manager = CreateManager())
			{
				var client = manager.GetClient();

				AssertClientHasHost(client, firstReadWriteHost);
			}
		}

        private static void AssertClientHasHost(IRedisClient client, string hostWithOptionalPort)
        {
            var parts = hostWithOptionalPort.Split(':');
            var port = parts.Length > 1 ? int.Parse(parts[1]) : RedisConfig.DefaultPort;

            Assert.That(client.Host, Is.EqualTo(parts[0]));
            Assert.That(client.Port, Is.EqualTo(port));
        }


		[Test]
		public void Does_loop_through_ReadWrite_hosts()
		{
            using (var manager = CreateManager())
			{
				var client1 = manager.GetClient();
				client1.Dispose();
				var client2 = manager.GetClient();
				var client3 = manager.GetClient();
				var client4 = manager.GetClient();
				var client5 = manager.GetClient();

				AssertClientHasHost(client1, hosts[0]);
				AssertClientHasHost(client2, hosts[1]);
				AssertClientHasHost(client3, hosts[2]);
				AssertClientHasHost(client4, hosts[3]);
				AssertClientHasHost(client5, hosts[0]);
			}
		}

		[Test]
		public void Can_have_different_pool_size_and_host_configurations()
		{
			var writeHosts = new[] { "readwrite1" };

            using (var manager = new RedisManagerPool(
                    writeHosts, 
                    new RedisPoolConfig { MaxPoolSize = 4 }))
			{
				//A poolsize of 4 will not block getting 4 clients
				using (var client1 = manager.GetClient())
				using (var client2 = manager.GetClient())
				using (var client3 = manager.GetClient())
				using (var client4 = manager.GetClient())
				{
					AssertClientHasHost(client1, writeHosts[0]);
					AssertClientHasHost(client2, writeHosts[0]);
					AssertClientHasHost(client3, writeHosts[0]);
					AssertClientHasHost(client4, writeHosts[0]);
				}
			}
		}

		[Test]
		public void Does_not_block_ReadWrite_clients_pool()
		{
            using (var manager = new RedisManagerPool(
                    hosts,
                    new RedisPoolConfig { MaxPoolSize = 4 }))
            {
				var delay = TimeSpan.FromSeconds(1);
				var client1 = manager.GetClient();
				var client2 = manager.GetClient();
				var client3 = manager.GetClient();
				var client4 = manager.GetClient();

                Assert.That(((RedisClient)client1).IsManagedClient, Is.True);
                Assert.That(((RedisClient)client2).IsManagedClient, Is.True);
                Assert.That(((RedisClient)client3).IsManagedClient, Is.True);
                Assert.That(((RedisClient)client4).IsManagedClient, Is.True); 

				Action func = delegate {
					Thread.Sleep(delay + TimeSpan.FromSeconds(0.5));
					client4.Dispose();
				};

#if !NET_CORE
				func.BeginInvoke(null, null);
#else
				System.Threading.Tasks.Task.Factory.StartNew(func);
#endif
				var start = DateTime.Now;

				var client5 = manager.GetClient();

                Assert.That(((RedisClient)client5).IsManagedClient, Is.False); //outside of pool

				Assert.That(DateTime.Now - start, Is.LessThan(delay));

				AssertClientHasHost(client1, hosts[0]);
				AssertClientHasHost(client2, hosts[1]);
				AssertClientHasHost(client3, hosts[2]);
				AssertClientHasHost(client4, hosts[3]);
				AssertClientHasHost(client5, hosts[0]);
			}
		}

		[Test]
		public void Can_support_64_threads_using_the_client_simultaneously()
		{
			const int noOfConcurrentClients = 64; //WaitHandle.WaitAll limit is <= 64
			var clientUsageMap = new Dictionary<string, int>();

			var clientAsyncResults = new List<IAsyncResult>();
            using (var manager = CreateManager())
			{
				for (var i = 0; i < noOfConcurrentClients; i++)
				{
					var clientNo = i;
					var action = (Action)(() => UseClient(manager, clientNo, clientUsageMap));
#if !NET_CORE
					clientAsyncResults.Add(action.BeginInvoke(null, null));
#else
					var f = System.Threading.Tasks.Task.Factory.StartNew(action);
					clientAsyncResults.Add(f);
#endif
				}
			}

			WaitHandle.WaitAll(clientAsyncResults.ConvertAll(x => x.AsyncWaitHandle).ToArray());

			Debug.WriteLine(TypeSerializer.SerializeToString(clientUsageMap));

			var hostCount = 0;
			foreach (var entry in clientUsageMap)
			{
				Assert.That(entry.Value, Is.GreaterThanOrEqualTo(5), "Host has unproportianate distrobution: " + entry.Value);
				Assert.That(entry.Value, Is.LessThanOrEqualTo(30), "Host has unproportianate distrobution: " + entry.Value);
				hostCount += entry.Value;
			}

			Assert.That(hostCount, Is.EqualTo(noOfConcurrentClients), "Invalid no of clients used");
		}

		private static void UseClient(IRedisClientsManager manager, int clientNo, Dictionary<string, int> hostCountMap)
		{
			using (var client = manager.GetClient())
			{
				lock (hostCountMap)
				{
					int hostCount;
					if (!hostCountMap.TryGetValue(client.Host, out hostCount))
					{
						hostCount = 0;
					}

					hostCountMap[client.Host] = ++hostCount;
				}

				Debug.WriteLine(String.Format("Client '{0}' is using '{1}'", clientNo, client.Host));
			}
		}

	}
}