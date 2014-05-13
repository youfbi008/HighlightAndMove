﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Outlining;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudio.Utilities;
using SimilarHighlight.SettingsStore;
using Microsoft.VisualStudio.Text.Tagging;

namespace SimilarHighlight.OverviewMargin.Implementation
{
    [Export(typeof(IWpfTextViewMarginProvider))]
    [Name(PredefinedOverviewMarginNames.Overview)]
    [MarginContainer(PredefinedMarginNames.VerticalScrollBarContainer)]
    [Order(After = PredefinedMarginNames.VerticalScrollBar)]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class OverviewMarginProvider : IWpfTextViewMarginProvider
    {
        [ImportMany]
        internal List<Lazy<IWpfTextViewMarginProvider, IWpfTextViewMarginMetadata>> _marginProviders;

        [Import]
        internal IScrollMapFactoryService _scrollMapFactory;

        [Import]
        internal IOutliningManagerService OutliningManagerService { get; private set; }

        [Import]
        internal ITextEditorFactoryService EditorFactory { get; private set; }

        [Import]
        internal IProjectionBufferFactoryService ProjectionFactory { get; private set; }

        [Import]
        internal IEditorOptionsFactoryService EditorOptionsFactoryService { get; private set; }

        [Import(AllowDefault = true)]
        internal ISettingsStore _settingsStore { get; set; }

        public bool LoadOption(IEditorOptions options, string optionName)
        {
            if (_settingsStore != null)
            {
                return _settingsStore.LoadOption(options, optionName);
            }
            return false;
        }

        public bool SaveOption(IEditorOptions options, string optionName)
        {
            if (_settingsStore != null)
            {
                return _settingsStore.SaveOption(options, optionName);
            }
            return false;
        }

        private IList<Lazy<IWpfTextViewMarginProvider, IWpfTextViewMarginMetadata>> _orderedMarginProviders;
        internal IList<Lazy<IWpfTextViewMarginProvider, IWpfTextViewMarginMetadata>> OrderedMarginProviders
        {
            get
            {
                if (_orderedMarginProviders == null)
                {
                    _orderedMarginProviders = Orderer.Order<IWpfTextViewMarginProvider, IWpfTextViewMarginMetadata>(_marginProviders);
                }

                return _orderedMarginProviders;
            }
        }

        /// <summary>
        /// Create an instance of the OverviewMargin in the specified <see cref="IWpfTextViewHost"/>.
        /// </summary>
        /// <param name="textViewHost">The <see cref="IWpfTextViewHost"/> in which the OverviewMargin will be displayed.</param>
        /// <returns>The newly created OverviewMargin.</returns>
        public IWpfTextViewMargin CreateMargin(IWpfTextViewHost textViewHost, IWpfTextViewMargin containerMargin)
        {
            return OverviewMargin.Create(textViewHost, containerMargin, this);
        }
    }
}