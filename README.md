C# Application Server - a.k.a. SharpBoss (**WIP**)
========================================

A simple C# Application Server inspired on Redhat JBoss Project.


Features
========

So far we have:

*   Working HTTP Server
*   Hot-Deploy / Un-deploy Assemblies
*   Log support
*   Multiple Applications
*   Exception Handling support with Debug Symbols Support (if provided on the app folder)
*   JSON Serializer for Object Return in REST calls
*   **Custom Exception Handlers**
*   Argument Deserialization for REST calls 

TODO
======

*   REST Path Param support (**WIP**)
*   Better threading for multiple apps
*   **NHibernate Services** Support with Automatic Session Open / Close / Exception Handler
*   Unit Tests to ensure everything is OK

How it works
=============

If you have ever used Redhat JBoss, it is pretty similar but for .NET. So basically you create a `Library Project` that uses the **ASAttrib** REST attributes to make REST Endpoint calls. 

The output `.dll` file should be put inside `apps/YOUR_APP_FOLDER` to be deployed. It can either be before executing the AppServer or after it (Hot-Deploy). The assemblies will be automatically copied to a folder called `deployed` to avoid filesystem locking issues. In this way, you can also update the `.dll` assembly anytime, and the AppServer will be automatically reload it. 

The AppServer handles unhandled exceptions by logging and returning the `StackTrace` of the exception. If a `.pdb` file is available, it will also shows the source file and line number of the crash.

How to use REST Attributes
==========================

This is actually pretty simple. Just create a Class and put the `REST("/mypath")` attribute on it to mark it as a REST Endpoint Class. In the methods of that class you can anotate with `GET("/endpoint")`, `POST("/endpoint")`, `PUT("/endpoint")`, `DELETE("/endpoint")`. The method should receive a `RestRequest` object that contains the Request Parameters and can return either `string` or a `object` that will be JSON serialized. The method can also throw an exception to indicate an error. The AppServer will return a HTTP Status Code 500 with the StackTrace as message. 

The SampleApp Example:

```cs
namespace SampleApp {
  [Rest("/hue")]
  public class MyRestSample {
    [GET("/test")]
    public string hueTest([QueryParam] string param0, [QueryParam] float param1) {
      return "GET TO HUEHUE with param: Param0(" + param0 + "), Param1(" + param1 +")";
    }
    
    [POST("/test")]
    public TestModel hueTest2(TestModel model) {
      model.count += 100;
      return model;
    }

    [GET("/exception-test")]
    public TestModel exceptionTest() {
      throw new NullReferenceException("Test of an Exception");
    }

    [GET("/custom-exception-test")]
    public TestModel customExceptionTest() {
      throw new CustomException("NOOOOOOOOOOOOOOOOOOOOO!");
    }
  }
}
```
This will create the endpoints that will be described in the next section.


How to use Custom Exception Handler
===================================

Also it is very simple. Just create a class that implements the interface `IRestExceptionHandler` and put a attribute `[RestExceptionHandler(typeof(TheExceptionYouWantToHandle))]` on it and you're good to go. Example:

```cs
namespace SampleApp {
  [RestExceptionHandler(typeof(CustomException))]
  public class MyCustomExceptionHandler : IRestExceptionHandler {
    public RestResult handleException(Exception e) {
      CustomException ce = (CustomException)e;    //  The Custom Exception handler will only be called with the correct type of exception

      RestResult result = new RestResult();
      result.ContentType = "text/plain";
      result.StatusCode = System.Net.HttpStatusCode.NotAcceptable;
      result.Result = Encoding.UTF8.GetBytes("Handling CustomException that has a message: " + ce.Message);

      return result;
    }
  }
}
```

Testing locally
===============

1. Compile the Solution
2. Create a folder `apps` in the `AppServer.exe` folder.
3. Create a folder `sampleapp` in `apps` folder
4. Copy the `SampleApp.dll` and `SampleApp.pdb` file from the `SampleApp` project to `apps/sampleapp` folder.
5. Run `AppServer.exe`

You will have some endpoints available:

*   `GET` **/sampleapp/hue/test?param0=SomeString&param1=30,5**
    *   This will return a string `"GET TO HUEHUE with param: Param0(SomeString), Param1(30,5)"` as `text/plain`
*   `POST` **/sampleapp/hue/test**
    * Post with a JSON like this: 
    ```json
      {
        "name":"Lucas",
        "count":10,
        "test":"HUEHUE"
      }
    ```
    * The response will be this:
    ```json
    {
      "name":"Lucas",
      "count":110,
      "test":"HUEHUE"
    }
    ```
*   `GET` **/sampleapp/hue/exception-test**
    *   This will throw a test `NullReferenceException` with the message `Test of an Exception`
*   `GET` **/sampleapp/hue/custom-exception-test**
    *   This will throw a test `CustomException` that will be handled by MyCustomExceptionHandler and will output `Handling CustomException that has a message: NOOOOOOOOOOOOOOOOOOOOO!`