using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Android;
using Android.App;
using Android.Media;
using Android.OS;
using Android.Runtime;
using Android.Support.Design.Widget;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Views;
using Java.Lang;
using Microsoft.Extensions.Logging;
using NSPersonalCloud;
using NSPersonalCloud.Apps.Album;

namespace SimpleAndroid
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, NavigationView.IOnNavigationItemSelectedListener
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);
            Android.Support.V7.Widget.Toolbar toolbar = FindViewById<Android.Support.V7.Widget.Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);

            FloatingActionButton fab = FindViewById<FloatingActionButton>(Resource.Id.fab);
            fab.Click += FabOnClick;

            DrawerLayout drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
            ActionBarDrawerToggle toggle = new ActionBarDrawerToggle(this, drawer, toolbar, Resource.String.navigation_drawer_open, Resource.String.navigation_drawer_close);
            drawer.AddDrawerListener(toggle);
            toggle.SyncState();

            NavigationView navigationView = FindViewById<NavigationView>(Resource.Id.nav_view);
            navigationView.SetNavigationItemSelectedListener(this);
        }

        public override void OnBackPressed()
        {
            DrawerLayout drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
            if(drawer.IsDrawerOpen(GravityCompat.Start))
            {
                drawer.CloseDrawer(GravityCompat.Start);
            }
            else
            {
                base.OnBackPressed();
            }
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.menu_main, menu);
            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            int id = item.ItemId;
            if (id == Resource.Id.action_settings)
            {
                return true;
            }

            return base.OnOptionsItemSelected(item);
        }

        private void FabOnClick(object sender, EventArgs eventArgs)
        {
            Task.Run(() => {

                try
                {

                    var my = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
                    var dic = Path.Combine(my, "TestConsoleApp", "webapps");
                    Directory.CreateDirectory(dic);
                    var loggerFactory = LoggerFactory.Create(builder => {
                        builder
                            .AddFilter("System", LogLevel.Warning);
                    });

                    var t1 = new SimpleConfigStorage(
                        Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                        "TestConsoleApp", Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)));
                    var pcservice = new PCLocalService(t1, loggerFactory, new VirtualFileSystem(t1.RootPath), dic);
                    Directory.CreateDirectory(dic);
                    pcservice.InstallApps().Wait();

                    pcservice.StartService();
                    var pc = pcservice.CreatePersonalCloud("test", "test1").Result;

                    Thread.Sleep(3000);
                    var routdir = pc.RootFS.EnumerateChildrenAsync("/").Result;
                    //using var mem = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("This is a test sentence."));
                    File.Delete(Path.Combine(dic, "content.txt"));
                    var fso = new FileStream(Path.Combine(dic, "content.txt"), FileMode.OpenOrCreate);
                    byte[] buf = new byte[1024 * 1024];
                    for (int i = 0; i < 1; i++)
                    {
                        fso.Write(buf);
                    }
                    fso.Dispose();
                    //File.WriteAllText(Path.Combine(dic, "content.txt"), "This is a test sentence.");
                    using var fs = new FileStream(Path.Combine(dic, "content.txt"), FileMode.Open);
                    try
                    {
                        pc.RootFS.DeleteAsync("/test1/tex.txt").AsTask().Wait();
                    }
                    catch (System.Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                    var fi = pc.RootFS.EnumerateChildrenAsync("/test1/").Result;

                    pc.RootFS.WriteFileAsync("/test1/tex.txt", fs).GetAwaiter().GetResult();

                    using var rfs = pc.RootFS.ReadFileAsync("/test1/tex.txt").Result;
                    for (int i = 0; i < 100; i++)
                    {
                        var read = rfs.Read(buf, 0, 1024 * 1024);
                        Console.WriteLine(read);
                    }
                }
                catch (System.Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            });
        }

        public bool OnNavigationItemSelected(IMenuItem item)
        {
            int id = item.ItemId;

            if (id == Resource.Id.nav_camera)
            {
                // Handle the camera action
            }
            else if (id == Resource.Id.nav_gallery)
            {

            }
            else if (id == Resource.Id.nav_slideshow)
            {

            }
            else if (id == Resource.Id.nav_manage)
            {

            }
            else if (id == Resource.Id.nav_share)
            {

            }
            else if (id == Resource.Id.nav_send)
            {

            }

            DrawerLayout drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
            drawer.CloseDrawer(GravityCompat.Start);
            return true;
        }
        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}

