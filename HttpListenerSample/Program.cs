using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


internal class HmUsedPortChecker
{
    static List<int> portsInUse;
    public static int GetAvailablePort(int beginPort, int endPort)
    {
        var ipGP = IPGlobalProperties.GetIPGlobalProperties();
        var tcpEPs = ipGP.GetActiveTcpListeners();
        var udpEPs = ipGP.GetActiveUdpListeners();
        portsInUse = tcpEPs.Concat(udpEPs).Select(p => p.Port).ToList();

        for (int port = beginPort; port <= endPort; ++port)
        {
            if (!portsInUse.Contains(port))
            {
                return port;
            }
        }

        return 0; // 空きポートが見つからない場合
    }
}



public class HttpListnerSample
{
    CancellationTokenSource cts;
    bool isRunning = false;

    HttpListener listener;

    int port = 0;

    dynamic onPostRequestFunc = null;
    dynamic onTextRequestFunc = null;

    public HttpListnerSample(dynamic onPostRequestFunc, dynamic onTextRequestFunc)
    {
        this.onPostRequestFunc = onPostRequestFunc;
        this.onTextRequestFunc = onTextRequestFunc;
    }


    public int Start(int portBGN, int portEND)
    {
        try
        {
            port = HmUsedPortChecker.GetAvailablePort(portBGN, portEND);
            if (port == 0)
            {
                Trace.WriteLine("${portBGN}～${portEND}の間に、使用可能なポートが見つかりませんでした。\r\n");
                return 0;
            }

            cts = new CancellationTokenSource();
            _ = Task.Run(() => StartTask(cts.Token), cts.Token);
        }
        catch (Exception e)
        {
            Trace.WriteLine(e.Message + "\r\n");
        }

        return port;
    }


    // この関数は、HTTPリスナーを作成し、リクエストを受け取るまで待機します。
    // リクエストを受け取ると、HTMLを返します。
    // この関数は、HmFetchTextServer.Start()から呼び出されます。

    private Task StartTask(CancellationToken cts)
    {

        try
        {

            // HTTPリスナー作成
            listener = new HttpListener();

            // リスナー設定
            listener.Prefixes.Clear();
            listener.Prefixes.Add($"http://localhost:{port}/");

            // リスナー開始
            listener.Start();

            isRunning = true;

            while (isRunning)
            {
                try
                {
                    if (cts.IsCancellationRequested)
                    {
                        break;
                    }


                    // リクエスト取得
                    HttpListenerContext context = listener.GetContext();

                    context.Response.Headers.Add("Access-Control-Allow-Origin", "*");

                    HttpListenerRequest request = context.Request;


                    // リクエストのHTTPメソッドがPOSTである場合のみ、リクエストボディからデータを読み取る
                    if (request.HttpMethod == "POST")
                    {
                        int status = 200;
                        using (StreamReader reader = new StreamReader(request.InputStream, Encoding.UTF8))
                        {
                            // リクエストボディから文字列を読み取る
                            string text = reader.ReadToEnd();

                            // 受信したテキスト文字列をコンソールに出力する
                            int? temp_status = onPostRequestFunc(text);
                            if (temp_status.HasValue)
                            {
                                status = temp_status.Value;
                            }
                        }

                        // レスポンス取得
                        HttpListenerResponse response = context.Response;

                        response.StatusCode = status;

                        response.Close();
                    }
                    else
                    {
                        // レスポンス取得
                        HttpListenerResponse response = context.Response;


                        // HTMLを表示する
                        if (request != null)
                        {
                            string hmtext = this.onTextRequestFunc();
                            byte[] text = Encoding.UTF8.GetBytes(hmtext);
                            response.ContentType = "text/plain; charset=utf-8";
                            response.ContentEncoding = Encoding.UTF8;
                            response.OutputStream.Write(text, 0, text.Length);
                        }
                        else
                        {
                            response.StatusCode = 404;
                        }

                        response.Close();
                    }
                }
                catch (HttpListenerException e)
                {
                    if (e.ErrorCode == 995)
                    {
                        // キャンセルされた場合は、例外が発生するので、無視する。
                    }
                    else
                    {
                        Trace.WriteLine(e.Message + "\r\n");
                    }
                }
                catch (OperationCanceledException e)
                {
                    // キャンセルされた場合は、例外が発生するので、無視する。
                }
                catch (ObjectDisposedException e)
                {
                    // キャンセルされた場合は、例外が発生するので、無視する。
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e.Message + "\r\n");
                }
            }

        }
        catch (Exception e)
        {
            Trace.WriteLine(e.Message + "\r\n");
        }

        return Task.CompletedTask;
    }

    public int Close()
    {
        try
        {

            _ = Task.Run(() => CloseTask());
        }
        catch (Exception e)
        {
            Trace.WriteLine(e.Message + "\r\n");
        }
        return 1;
    }

    public void OnReleaseObject(int reason = 0)
    {
        Close();
    }

    // この関数は、HTTPリスナーを停止します。
    // この関数は、HmFetchTextServer.Close()から呼び出されます。

    private void CloseTask()
    {
        if (listener != null)
        {
            isRunning = false;
            cts.Cancel();
            listener.Stop();
            listener.Close();
        }
    }
}