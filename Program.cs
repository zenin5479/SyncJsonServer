using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace SyncJsonServer
{
   public class Item
   {
      public int Id { get; set; }
      public string Name { get; set; }
      public double Price { get; set; }
   }

   public class HttpServer
   {
      private readonly HttpListener _listener;
      private readonly string _url;
      private readonly List<Item> _items = new List<Item>();
      private int _nextId = 1;

      public HttpServer(string url)
      {
         _url = url;
         _listener = new HttpListener();
         _listener.Prefixes.Add(url);
      }

      public void Start()
      {
         _listener.Start();
         Console.WriteLine($"Server started at {_url}");

         while (true)
         {
            try
            {
               var context = _listener.GetContext();
               ProcessRequest(context);
            }
            catch (Exception ex)
            {
               Console.WriteLine($"Error: {ex.Message}");
            }
         }
      }

      private void ProcessRequest(HttpListenerContext context)
      {
         var request = context.Request;
         var response = context.Response;

         try
         {
            Console.WriteLine($"{request.HttpMethod} {request.Url?.AbsolutePath}");

            switch (request.HttpMethod)
            {
               case "GET":
                  HandleGet(request, response);
                  break;
               case "POST":
                  HandlePost(request, response);
                  break;
               case "PUT":
                  HandlePut(request, response);
                  break;
               case "DELETE":
                  HandleDelete(request, response);
                  break;
               default:
                  SendResponse(response, 405, new { error = "Method not allowed" });
                  break;
            }
         }
         catch (Exception ex)
         {
            SendResponse(response, 500, new { error = ex.Message });
         }
         finally
         {
            response.Close();
         }
      }

      private void HandleGet(HttpListenerRequest request, HttpListenerResponse response)
      {
         var path = request.Url?.AbsolutePath.Trim('/');

         if (string.IsNullOrEmpty(path) || path == "api/items")
         {
            SendResponse(response, 200, _items);
            return;
         }

         if (path.StartsWith("api/items/"))
         {
            var idStr = path.Substring("api/items/".Length);
            if (int.TryParse(idStr, out int id))
            {
               var item = _items.Find(i => i.Id == id);
               if (item != null)
               {
                  SendResponse(response, 200, item);
               }
               else
               {
                  SendResponse(response, 404, new { error = "Item not found" });
               }
            }
            else
            {
               SendResponse(response, 400, new { error = "Invalid ID" });
            }
            return;
         }

         SendResponse(response, 404, new { error = "Not found" });
      }

      private void HandlePost(HttpListenerRequest request, HttpListenerResponse response)
      {
         if (request.Url?.AbsolutePath.Trim('/') != "api/items")
         {
            SendResponse(response, 404, new { error = "Not found" });
            return;
         }

         using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
         var body = reader.ReadToEnd();

         var newItem = JsonConvert.DeserializeObject<Item>(body);
         if (newItem == null || string.IsNullOrEmpty(newItem.Name))
         {
            SendResponse(response, 400, new { error = "Invalid item data" });
            return;
         }

         newItem.Id = _nextId++;
         _items.Add(newItem);

         SendResponse(response, 201, newItem);
      }

      private void HandlePut(HttpListenerRequest request, HttpListenerResponse response)
      {
         var path = request.Url?.AbsolutePath.Trim('/');
         if (!path.StartsWith("api/items/"))
         {
            SendResponse(response, 404, new { error = "Not found" });
            return;
         }

         var idStr = path.Substring("api/items/".Length);
         if (!int.TryParse(idStr, out int id))
         {
            SendResponse(response, 400, new { error = "Invalid ID" });
            return;
         }

         var existingItem = _items.Find(i => i.Id == id);
         if (existingItem == null)
         {
            SendResponse(response, 404, new { error = "Item not found" });
            return;
         }

         using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
         var body = reader.ReadToEnd();

         var updatedItem = JsonConvert.DeserializeObject<Item>(body);
         if (updatedItem == null || string.IsNullOrEmpty(updatedItem.Name))
         {
            SendResponse(response, 400, new { error = "Invalid item data" });
            return;
         }

         existingItem.Name = updatedItem.Name;
         existingItem.Price = updatedItem.Price;

         SendResponse(response, 200, existingItem);
      }

      private void HandleDelete(HttpListenerRequest request, HttpListenerResponse response)
      {
         var path = request.Url?.AbsolutePath.Trim('/');
         if (!path.StartsWith("api/items/"))
         {
            SendResponse(response, 404, new { error = "Not found" });
            return;
         }

         var idStr = path.Substring("api/items/".Length);
         if (!int.TryParse(idStr, out int id))
         {
            SendResponse(response, 400, new { error = "Invalid ID" });
            return;
         }

         var item = _items.Find(i => i.Id == id);
         if (item == null)
         {
            SendResponse(response, 404, new { error = "Item not found" });
            return;
         }

         _items.Remove(item);
         SendResponse(response, 200, new { message = "Item deleted" });
      }

      private void SendResponse(HttpListenerResponse response, int statusCode, object data)
      {
         var json = JsonConvert.SerializeObject(data, Formatting.Indented);
         var buffer = Encoding.UTF8.GetBytes(json);

         response.StatusCode = statusCode;
         response.ContentType = "application/json";
         response.ContentLength64 = buffer.Length;
         response.ContentEncoding = Encoding.UTF8;

         response.OutputStream.Write(buffer, 0, buffer.Length);
      }

      public void Stop()
      {
         _listener.Stop();
         _listener.Close();
      }
   }

   class Program
   {
      static void Main(string[] args)
      {
         var server = new HttpServer("http://127.0.0.1:8080/");

         try
         {
            server.Start();
         }
         catch (HttpListenerException ex)
         {
            Console.WriteLine(string.Format("Failed to start server: {0}", ex.Message));
            Console.WriteLine("You may need to run as administrator or add URL ACL:");
            Console.WriteLine($"netsh http add urlacl url=http://+:8080/ user={Environment.UserName}");
         }
         catch (Exception ex)
         {
            Console.WriteLine($"Error: {ex.Message}");
         }
      }
   }
}