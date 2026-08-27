using System;
using System.Reflection;
using KSP.Game.StartupFlow;
using UnityEngine;

namespace ReduxTestHarness
{
    /// <summary>
    /// Test-launch-only handling for KSP2's non-legal photosensitivity page.
    /// EULA, privacy-policy, and terms-of-service pages are intentionally never
    /// accepted or dismissed here.
    /// </summary>
    internal static class StartupDialogAutomation
    {
        private static readonly FieldInfo PhotosensitivityField =
            typeof(LegalMenu).GetField(
                "_photosensitivity",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo FinishStartupFlowMethod =
            typeof(LegalMenu).GetMethod(
                "FinishStartupFlow",
                BindingFlags.Instance | BindingFlags.NonPublic);

        public static bool IsPhotosensitivityWarningVisible()
        {
            LegalMenu menu;
            return TryFindVisiblePhotosensitivityWarning(out menu);
        }

        public static bool TryDismissPhotosensitivityWarning(out string error)
        {
            error = null;
            LegalMenu menu;
            if (!TryFindVisiblePhotosensitivityWarning(out menu))
            {
                return false;
            }

            // A test harness must never implicitly accept legal agreements.
            // KSP2's own button reaches FinishStartupFlow only when all three
            // agreements are already accepted at their current versions.
            if (!menu.AreAllLegalTextsAccepted() ||
                !menu.IsAcceptanceForAllLegalTextsLatestVersion())
            {
                error = "A legal agreement requires attention; only the photosensitivity warning can be dismissed automatically.";
                return false;
            }

            if (FinishStartupFlowMethod == null)
            {
                error = "KSP2 LegalMenu.FinishStartupFlow was not found in this player build.";
                return false;
            }

            try
            {
                // This is the photosensitivity page's final transition. Unlike
                // OnLegalAccepted, it cannot write legal-acceptance preferences.
                FinishStartupFlowMethod.Invoke(menu, null);
                return true;
            }
            catch (Exception exception)
            {
                Exception cause = exception is TargetInvocationException &&
                    exception.InnerException != null
                    ? exception.InnerException
                    : exception;
                error = "Could not dismiss KSP2's photosensitivity warning: " + cause.Message;
                return false;
            }
        }

        private static bool TryFindVisiblePhotosensitivityWarning(out LegalMenu menu)
        {
            menu = null;
            if (PhotosensitivityField == null)
            {
                return false;
            }

            LegalMenu[] menus = Resources.FindObjectsOfTypeAll<LegalMenu>();
            for (int index = 0; index < menus.Length; index++)
            {
                LegalMenu candidate = menus[index];
                if (candidate == null || !candidate.isActiveAndEnabled ||
                    candidate.gameObject == null || !candidate.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Behaviour warning = PhotosensitivityField.GetValue(candidate) as Behaviour;
                if (warning != null && warning.isActiveAndEnabled &&
                    warning.gameObject != null && warning.gameObject.activeInHierarchy)
                {
                    menu = candidate;
                    return true;
                }
            }
            return false;
        }
    }
}
