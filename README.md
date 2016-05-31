C# Application Server - a.k.a. SharpBoss (**WIP**)
========================================

A simple C# Application Server inspired on Redhat JBoss Project.


Features
========

So far we have:

*	Working HTTP Server
*	Hot-Deploy / Un-deploy Assemblies
*	Log support
*	Multiple Applications
*	Exception Handling support with Debug Symbols Support (if provided on the app folder)
*	JSON Serializer for Object Return in REST calls
*   **Custom Exception Handlers**

TODO
======

*	Argument Deserialization for REST calls 
*	REST Path Param support
*	Better threading for multiple apps
*	**NHibernate Services** Support with Automatic Session Open / Close / Exception Handler
*	Unit Tests to ensure everything is OK

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
        public string hueTest(RestRequest request) {
          return "GET TO HUEHUE";
        }
        
        [POST("/test")]
        public TestModel hueTest2(RestRequest request) {
          TestModel x = new TestModel();
          x.name = "Lucas";
          x.count = 10;
          x.test = "HUEHUE";
          return x;
        }
        [GET("/exception-test")]
        public TestModel exceptionTest(RestRequest request) {
          throw new NullReferenceException("Test of an Exception");
        }
    }
}
```

This will create the endpoints that will be described in the next section.

Testing locally
===============

1. Compile the Solution
2. Create a folder `apps` in the `AppServer.exe` folder.
3. Create a folder `sampleapp` in `apps` folder
4. Copy the `SampleApp.dll` and `SampleApp.pdb` file from the `SampleApp` project to `apps/sampleapp` folder.
5. Run `AppServer.exe`

You will have some endpoints available:

*   `GET` **/sampleapp/hue/test**
    *   This will return a string `"GET TO HUEHUE"` as `text/plain`
*   `POST` **/sampleapp/hue/test**
    *   This will return a JSON of `TestModel` with the fields `name` = `"Lucas"`, `count` = `10` and `test` = `"HUEHUE"`
*   `GET` **/sampleapp/hue/exception-test**
    *   This will throw a test `NullReferenceException` with the message `Test of an Exception`