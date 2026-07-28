using Awayra.App.Services;
using Awayra.App.Tests.Support;
using Awayra.App.ViewModels;
using Awayra.Core.Services;

namespace Awayra.App.Tests;

[TestClass]
public sealed class OverlayGlassViewModelTests
{
    [TestMethod]
    public void ApplyGlassClarity_UpdatesTintAndBlur()
    {
        StaTestContext.Run(() =>
        {
            var viewModel = new OverlayViewModel
            {
                GlassClarity = 20
            };

            viewModel.ApplyGlassClarity(125);

            Assert.AreEqual(125, viewModel.GlassClarity);
            Assert.AreEqual(0.0, viewModel.BackgroundTintOpacity, 0.001);
            Assert.AreEqual(13.0, viewModel.BlurRadius, 0.001);
            Assert.AreEqual(1.0, viewModel.ContentOpacity, 0.001);
        });
    }

    [TestMethod]
    public void LocalizationService_DefaultsToEnglish()
    {
        StaTestContext.Run(() =>
        {
            var localization = new LocalizationService();
            localization.Apply();

            Assert.AreEqual("en", localization.CurrentCultureName);
        });
    }
}
