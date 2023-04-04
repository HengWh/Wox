using Api;
using Echo;
using Google.Protobuf;
using Grpc.Core;
using System.Diagnostics;
using System.Text;

namespace SearchTestClient
{
    public static class FzfTest
    {
        private static ApiService.ApiServiceClient _api;
        private static Echo.Echo.EchoClient _eco;
        static FzfTest()
        {
            //GRPC client
            var apiChannel = new Channel("127.0.0.1:38999", ChannelCredentials.Insecure);
            _api = new ApiService.ApiServiceClient(apiChannel);

            var ecoChannel = new Channel("127.0.0.1:8901", ChannelCredentials.Insecure);
            _eco = new Echo.Echo.EchoClient(ecoChannel);
        }

        public static void UpdatePath(string path)
        {
            try
            {
                var updateRequest = new UpdateRequest();
                var args = new UpdateRequest.Types.UpdateArgs()
                {
                    DbIdx = 1,
                    Key = 1,
                    Val = PackValue(path, false),
                    Deleted = false
                };
                updateRequest.Args.Add(args);
                var response = _api.Update(updateRequest);
                Console.WriteLine($"Update path finish.path is {path}. Response is:{response}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Update failed.\n{ex}");
            }
        }

        public static void Search(string[] items)
        {
            try
            {
                //Search
                SearchRequest request = new SearchRequest();
                request.WithPos = true;
                request.Flags = 3; //1-OnlyFiles  2-OnlyDirs  3-All
                request.PrefixMask = "";
                var terms = items.Select(p => new SearchRequest.Types.QueryTerm() { Term = p, CaseSensitive = false });
                request.Terms.AddRange(terms);

                using var response = _api.Search(request);

                while (response.ResponseStream.MoveNext().Result)
                {
                    var serchResponse = response.ResponseStream.Current;
                    foreach (var item in serchResponse.Results)
                    {
                        Console.WriteLine($"Receive Search Response:Key={item.Key},Val={UnpackValue(item.Val)}.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Search failed.\n{ex}");
            }
        }

        public static void SearchSync(string[] items)
        {
            try
            {
                //Search
                SearchRequest request = new SearchRequest();
                request.WithPos = true;
                request.Flags = 3; //1-OnlyFiles  2-OnlyDirs  3-All
                request.PrefixMask = "";
                var terms = items.Select(p => new SearchRequest.Types.QueryTerm() { Term = p, CaseSensitive = false });
                request.Terms.AddRange(terms);

                var response = _api.SearchSync(request);
                foreach (var result in response.Results)
                {
                    Console.WriteLine($"Receive Search Response:Key={result.Key},Val={UnpackValue(result.Val)}.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Search failed.\n{ex}");
            }
        }

        public static void Echo()
        {
            try
            {

                var response = _eco.Echo(new EchoRequest() { Message = "Hello Echo" });
                Console.WriteLine($"Echo finish. Response is:{response}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Echo failed.\n{ex}");
            }
        }

        public static void EchoStream()
        {
            try
            {
                var stop = Stopwatch.StartNew();
                Console.WriteLine("EchoStream start");
                var cts = new CancellationTokenSource();
                var response = _eco.StreamingEcho(new EchoRequest() { Message = "Hello Echo Stream" });
                Console.WriteLine($"EchoStream get response. Time span is {stop.ElapsedMilliseconds}ms");
                stop.Restart();
                int i = 0;
                while (response.ResponseStream.MoveNext().Result)
                {
                    Console.WriteLine($"EchoStream response stream move next. Time span is {stop.ElapsedMilliseconds}ms");
                    stop.Restart();
                    i++;
                    var serchResponse = response.ResponseStream.Current;
                    Console.WriteLine($"Receive Search Response:{serchResponse.Message}. Time span {stop.ElapsedMilliseconds}ms");
                    stop.Restart();
                    if (i == 10)
                    {
                        Console.WriteLine($"Receive Search Response more than 10. We can cancel it.");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Echo stream failed.\n{ex}");
            }
        }

        private static ByteString PackValue(string path, bool isDir)
        {
            var pathBytes = Encoding.UTF8.GetBytes(path);
            var bytes = new byte[pathBytes.Length + 1];
            bytes[0] = (byte)(isDir ? 1 : 0);
            pathBytes.CopyTo(bytes, 1);
            return ByteString.CopyFrom(bytes);
        }

        private static (string path, bool isDir) UnpackValue(ByteString data)
        {
            var bytes = data.ToByteArray();
            var isDir = bytes[0] == 1;
            var path = Encoding.UTF8.GetString(bytes[0..]);
            return (path, isDir);
        }
    }
}
