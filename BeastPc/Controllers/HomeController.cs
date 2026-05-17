using System.Web.Mvc;

namespace BeastPc.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        /// <summary>Shop catalog (ready-to-ship PCs).</summary>
        public ActionResult Shop()
        {
            return View();
        }

        /// <summary>Legacy URL — permanent redirect to Shop.</summary>
        public ActionResult Builds()
        {
            return RedirectToActionPermanent("Shop");
        }

        public ActionResult About()
        {
            return View();
        }

        public ActionResult Contact()
        {
            return View();
        }

        public ActionResult Checkout()
        {
            return View();
        }
    }
}
