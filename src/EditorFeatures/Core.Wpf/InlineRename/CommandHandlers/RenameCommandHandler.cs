﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Microsoft.CodeAnalysis.Notification;
using Microsoft.CodeAnalysis.ErrorReporting;
using Microsoft.CodeAnalysis.Extensions;
using Microsoft.CodeAnalysis.Telemetry;

#if !COCOA
using System.Linq;
#endif

namespace Microsoft.CodeAnalysis.Editor.Implementation.InlineRename
{
    [Export(typeof(ICommandHandler))]
    [ContentType(ContentTypeNames.RoslynContentType)]
    [ContentType(ContentTypeNames.XamlContentType)]
    [Name(PredefinedCommandHandlerNames.Rename)]
    // Line commit and rename are both executed on Save. Ensure any rename session is committed
    // before line commit runs to ensure changes from both are correctly applied.
    [Order(Before = PredefinedCommandHandlerNames.Commit)]
    // Commit rename before invoking command-based refactorings
    [Order(Before = PredefinedCommandHandlerNames.ChangeSignature)]
    [Order(Before = PredefinedCommandHandlerNames.ExtractInterface)]
    [Order(Before = PredefinedCommandHandlerNames.EncapsulateField)]
    internal partial class RenameCommandHandler : AbstractRenameCommandHandler
    {
        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public RenameCommandHandler(IThreadingContext threadingContext, InlineRenameService renameService)
            : base(threadingContext, renameService)
        {
        }

#if !COCOA
        protected override bool DashboardShouldReceiveKeyboardNavigation(ITextView textView)
            => GetDashboard(textView) is { } dashboard && dashboard.ShouldReceiveKeyboardNavigation;

        protected override void SetFocusToTextView(ITextView textView)
        {
            (textView as IWpfTextView)?.VisualElement.Focus();
        }

        protected override void SetFocusToDashboard(ITextView textView)
        {
            if (GetDashboard(textView) is { } dashboard)
            {
                dashboard.Focus();
            }
        }

        protected override void SetDashboardFocusToNextElement(ITextView textView)
        {
            if (GetDashboard(textView) is { } dashboard)
            {
                dashboard.FocusNextElement();
            }
        }

        protected override void SetDashboardFocusToPreviousElement(ITextView textView)
        {
            if (GetDashboard(textView) is { } dashboard)
            {
                dashboard.FocusNextElement();
            }
        }

        private static Dashboard? GetDashboard(ITextView textView)
        {
            // If our adornment layer somehow didn't get composed, GetAdornmentLayer will throw.
            // Don't crash if that happens.
            try
            {
                var adornment = ((IWpfTextView)textView).GetAdornmentLayer("RoslynRenameDashboard");
                return adornment.Elements.Any()
                    ? adornment.Elements[0].Adornment as Dashboard
                    : null;
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        protected override void Commit(InlineRenameSession activeSession, ITextView textView)
        {
            try
            {
                base.Commit(activeSession, textView);
            }
            catch (NotSupportedException ex)
            {
                // Session.Commit can throw if it can't commit
                // rename operation.
                // handle that case gracefully
                var notificationService = activeSession.Workspace.Services.GetService<INotificationService>();
                notificationService?.SendNotification(ex.Message, title: EditorFeaturesResources.Rename, severity: NotificationSeverity.Error);
            }
            catch (Exception ex) when (FatalError.ReportAndCatch(ex))
            {
                // Show a nice error to the user via an info bar
                var errorReportingService = activeSession.Workspace.Services.GetService<IErrorReportingService>();
                if (errorReportingService is null)
                {
                    return;
                }

                errorReportingService.ShowGlobalErrorInfo(
                    message: string.Format(EditorFeaturesWpfResources.Error_performing_rename_0, ex.Message),
                    TelemetryFeatureName.InlineRename,
                    ex,
                    new InfoBarUI(
                        WorkspacesResources.Show_Stack_Trace,
                        InfoBarUI.UIKind.HyperLink,
                        () => errorReportingService.ShowDetailedErrorInfo(ex), closeAfterAction: true));
            }
        }
#else
        protected override bool DashboardShouldReceiveKeyboardNavigation(ITextView textView)
            => false;

        protected override void SetFocusToTextView(ITextView textView)
        {
            // No action taken for Cocoa
        }

        protected override void SetFocusToDashboard(ITextView textView)
        {
            // No action taken for Cocoa
        }

        protected override void SetDashboardFocusToNextElement(ITextView textView)
        {
            // No action taken for Cocoa
        }

        protected override void SetDashboardFocusToPreviousElement(ITextView textView)
        {
            // No action taken for Cocoa
        }
#endif
    }
}
