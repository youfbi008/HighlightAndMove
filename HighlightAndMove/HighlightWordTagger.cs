﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using EnvDTE;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace HighlightAndMove
{
    internal class HighlightWordTagger : ITagger<HighlightWordTag>
    {
        IWpfTextView View { get; set; }
        ITextBuffer SourceBuffer { get; set; }
        ITextSearchService TextSearchService { get; set; }
        ITextStructureNavigator TextStructureNavigator { get; set; }
        NormalizedSnapshotSpanCollection WordSpans { get; set; }
        SnapshotSpan? CurrentWord { get; set; }
        SnapshotPoint RequestedPoint { get; set; }
        EnvDTE.Document document { get; set; }
        object updateLock = new object();

        public HighlightWordTagger(IWpfTextView view, ITextBuffer sourceBuffer, ITextSearchService textSearchService,
ITextStructureNavigator textStructureNavigator, EnvDTE.Document document)
        {
            this.View = view;
            this.document = document;
       //     this.View.VisualElement.left.MouseDown += OnLeftMouseButtonDown;
            this.View.VisualElement.PreviewKeyUp += VisualElement_PreviewKeyUp;
            this.SourceBuffer = sourceBuffer;
            this.TextSearchService = textSearchService;
            this.TextStructureNavigator = textStructureNavigator;
            this.WordSpans = new NormalizedSnapshotSpanCollection();
            this.CurrentWord = null;
            this.View.Caret.PositionChanged += CaretPositionChanged;
       //     this.View.LayoutChanged += ViewLayoutChanged;
        }

        void VisualElement_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) && Keyboard.IsKeyDown(Key.LeftAlt))
            {
                if (e.Key == Key.Left)
                {
                    WlSelect("bwd");
                }
                else if (e.Key == Key.Right)
                {
                    WlSelect("fwd");
                }
            }
        //    throw new NotImplementedException();
        }

        void OnLeftMouseButtonDown(object sender, RoutedEventArgs e)
        {
            if ((Keyboard.PrimaryDevice.Modifiers & ModifierKeys.Alt) != ModifierKeys.Alt)
                return;

            // Double click
            if ((e as MouseButtonEventArgs).ClickCount == 2)
            {
                UpdateAtCaretPosition(View.Caret.Position);
            }
        }

        private void OnKeyUp(object sender, RoutedEventArgs e)
        {
            //if ((e as KeyEventArgs).ctrl == true)
            //{
            //    if ((e as KeyEventArgs)..key == Key.Left) {
            //        WlSelect("fwd");
            //    }
            //    else if ((e as KeyEventArgs).Key == Key.Right)
            //    {
            //        WlSelect("bwd");
            //    }
            //}
        }

        //void ViewLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        //{
        //    // If a new snapshot wasn't generated, then skip this layout
        //    if (e.NewSnapshot != e.OldSnapshot)
        //    {
        //        UpdateAtCaretPosition(View.Caret.Position);
        //    }
        //}

        void CaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
        {
            
                UpdateAtCaretPosition(e.NewPosition);
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        void UpdateAtCaretPosition(CaretPosition caretPosition)
        {
            SnapshotPoint? point = caretPosition.Point.GetPoint(SourceBuffer, caretPosition.Affinity);
            
            if (!point.HasValue)
                return;

            // If the new caret position is still within the current word (and on the same snapshot), we don't need to check it
            if (CurrentWord.HasValue
                && CurrentWord.Value.Snapshot == View.TextSnapshot
                && point.Value >= CurrentWord.Value.Start
                && point.Value <= CurrentWord.Value.End)
            {
                return;
            }

            RequestedPoint = point.Value;
            UpdateWordAdornments();
        }

        void UpdateWordAdornments()
        {
            SnapshotPoint currentRequest = RequestedPoint;
            List<SnapshotSpan> wordSpans = new List<SnapshotSpan>();
            //Find all words in the buffer like the one the caret is on
            TextExtent word = TextStructureNavigator.GetExtentOfWord(currentRequest);


            bool foundWord = true;
            //If we've selected something not worth highlighting, we might have missed a "word" by a little bit
            if (!WordExtentIsValid(currentRequest, word))
            {
                //Before we retry, make sure it is worthwhile
                if (word.Span.Start != currentRequest
                     || currentRequest == currentRequest.GetContainingLine().Start
                     || char.IsWhiteSpace((currentRequest - 1).GetChar()))
                {
                    foundWord = false;
                }
                else
                {
                    // Try again, one character previous. 
                    //If the caret is at the end of a word, pick up the word.
                    word = TextStructureNavigator.GetExtentOfWord(currentRequest - 1);

                    //If the word still isn't valid, we're done
                    if (!WordExtentIsValid(currentRequest, word))
                        foundWord = false;
                }
            }

            if (!foundWord)
            {
                //If we couldn't find a word, clear out the existing markers
                SynchronousUpdate(currentRequest, new NormalizedSnapshotSpanCollection(), null);
                return;
            }

            SnapshotSpan currentWord = word.Span;
                       

            if (currentWord.GetText().IndexOf("WriteLine") < 0)
            {
                //SnapshotSpan preSpan = TextStructureNavigator.GetSpanOfPreviousSibling(currentWord);
                //SnapshotSpan nextSpan = TextStructureNavigator.GetSpanOfNextSibling(currentWord);

                //string adjText = preSpan.GetText() + nextSpan.GetText();

                return;
            }

            //If this is the current word, and the caret moved within a word, we're done.
            if (CurrentWord.HasValue && currentWord == CurrentWord)
                return;


            //Find the new spans
            //FindData findData = new FindData(currentWord.GetText(), currentWord.Snapshot);
            //findData.FindOptions = FindOptions.WholeWord | FindOptions.MatchCase;

            const string startPattenText = "(\\.)(\\s*)(WriteLine)(\\s*)(\\()(\\s*)(\")";
            const string endPattenText = "(\")(\\s*)(\\))";

            FindData startFindData = new FindData(startPattenText, currentWord.Snapshot);

            FindData endFindData = new FindData(endPattenText, currentWord.Snapshot);

            startFindData.FindOptions = FindOptions.UseRegularExpressions;

            ICollection<SnapshotSpan> tmpSpanAll = TextSearchService.FindAll(startFindData);
            ICollection<SnapshotSpan> tmpNewSpanAll = new List<SnapshotSpan>();
            foreach (SnapshotSpan tmpSpan in tmpSpanAll)
            {
                SnapshotSpan? newSpan = GetExpectedSpan(tmpSpan, endFindData);
                if (newSpan != null)
                {
                    tmpNewSpanAll.Add((SnapshotSpan)newSpan);
                }
            }

            //foreach (SnapshotSpan tmpNewTxt in tmpNexTxtAll)
            //{
            //    tmpTxtAll.Add(tmpNewTxt);
            //}

            wordSpans.AddRange(tmpNewSpanAll);

            //If another change hasn't happened, do a real update
            if (currentRequest == RequestedPoint)
                SynchronousUpdate(currentRequest, new NormalizedSnapshotSpanCollection(wordSpans), currentWord);
        }
        static bool WordExtentIsValid(SnapshotPoint currentRequest, TextExtent word)
        {
            return word.IsSignificant
                && currentRequest.Snapshot.GetText(word.Span).Any(c => char.IsLetter(c));
        }

        private SnapshotSpan? GetExpectedSpan(SnapshotSpan tmpSpan, FindData findData)
        {
            const string endPattenText = "(\")(\\s*)(\\))";
            SnapshotSpan? endSpan = ((ITextSearchService2)TextSearchService).Find(tmpSpan.End, endPattenText, FindOptions.UseRegularExpressions);

            if (endSpan == null) {

                return null;
            }
            SnapshotSpan expectedSpan = new SnapshotSpan(tmpSpan.End, ((SnapshotSpan)endSpan).Start);

            return expectedSpan;
        }

        void SynchronousUpdate(SnapshotPoint currentRequest, NormalizedSnapshotSpanCollection newSpans, SnapshotSpan? newCurrentWord)
        {
            lock (updateLock)
            {
                if (currentRequest != RequestedPoint)
                    return;

                WordSpans = newSpans;
                CurrentWord = newCurrentWord;

                var tempEvent = TagsChanged;
                if (tempEvent != null)
                    tempEvent(this, new SnapshotSpanEventArgs(new SnapshotSpan(SourceBuffer.CurrentSnapshot, 0, SourceBuffer.CurrentSnapshot.Length)));
            }
        }

        public IEnumerable<ITagSpan<HighlightWordTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (CurrentWord == null)
                yield break;

            // Hold on to a "snapshot" of the word spans and current word, so that we maintain the same
            // collection throughout
            SnapshotSpan currentWord = CurrentWord.Value;
            NormalizedSnapshotSpanCollection wordSpans = WordSpans;

            if (spans.Count == 0 || WordSpans.Count == 0)
                yield break;

            // If the requested snapshot isn't the same as the one our words are on, translate our spans to the expected snapshot
            if (spans[0].Snapshot != wordSpans[0].Snapshot)
            {
                wordSpans = new NormalizedSnapshotSpanCollection(
                    wordSpans.Select(span => span.TranslateTo(spans[0].Snapshot, SpanTrackingMode.EdgeExclusive)));

                currentWord = currentWord.TranslateTo(spans[0].Snapshot, SpanTrackingMode.EdgeExclusive);
            }

            // First, yield back the word the cursor is under (if it overlaps)
            // Note that we'll yield back the same word again in the wordspans collection;
            // the duplication here is expected.
            // It's not necessary for current needs.
            //if (spans.OverlapsWith(new NormalizedSnapshotSpanCollection(currentWord)))
            //    yield return new TagSpan<HighlightWordTag>(currentWord, new HighlightWordTag());

            // Second, yield all the other words in the file
            foreach (SnapshotSpan span in NormalizedSnapshotSpanCollection.Overlap(spans, wordSpans))
            {
                yield return new TagSpan<HighlightWordTag>(span, new HighlightWordTag());
            }
        }

        private void WlSelect(string selectType)
        {
            const string startPattenText = "(\\.)(\\s*)(WriteLine)(\\s*)(\\()(\\s*)(\")"; // (Console)(\s*)(\.)(\s*)(WriteLine)(\s*)(\()(\s*)(\")

            const string endPattenText = "(\")(\\s*)(\\))";

            TextSelection selected = this.document.Selection;

            if (selected != null)
            {
                if (selectType == "fwd")
                {

                    int selectionStartAbsoluteOffset = 0;
                    int selectionEndAbsoluteOffset = 0;
                    int tmpSelectionStartAbsoluteOffset = 0;
                    int tmpSelectionEndAbsoluteOffset = 0;

                    // Save the current selection:
                    tmpSelectionStartAbsoluteOffset = selected.TopPoint.AbsoluteCharOffset;
                    tmpSelectionEndAbsoluteOffset = selected.BottomPoint.AbsoluteCharOffset;
                    if (selected.Text.Length > 0)
                    {
                        selected.MoveToAbsoluteOffset(tmpSelectionStartAbsoluteOffset, false);
                    }

                    if (selected.FindPattern(startPattenText, (int)vsFindOptions.vsFindOptionsRegularExpression))
                    {
                        selectionStartAbsoluteOffset = selected.BottomPoint.AbsoluteCharOffset;

                        if (selected.FindPattern(endPattenText, (int)vsFindOptions.vsFindOptionsRegularExpression))
                        {
                            selectionEndAbsoluteOffset = selected.TopPoint.AbsoluteCharOffset;

                            // Restore the original selection:
                            selected.MoveToAbsoluteOffset(selectionStartAbsoluteOffset, false);
                            selected.MoveToAbsoluteOffset(selectionEndAbsoluteOffset, true);
                        }
                        else
                        {
                            selected.MoveToAbsoluteOffset(tmpSelectionStartAbsoluteOffset, false);
                            selected.MoveToAbsoluteOffset(tmpSelectionEndAbsoluteOffset, true);
                        }
                    }
                    else
                    {
                        selected.MoveToAbsoluteOffset(tmpSelectionEndAbsoluteOffset, true);
                    }
                }
                else if (selectType == "bwd")
                {
                    int tmpSelectionStartAbsoluteOffset = 0;
                    int tmpSelectionEndAbsoluteOffset = 0;

                    // Save the current selection:
                    tmpSelectionStartAbsoluteOffset = selected.TopPoint.AbsoluteCharOffset;
                    tmpSelectionEndAbsoluteOffset = selected.BottomPoint.AbsoluteCharOffset;

                    int selectionStartAbsoluteOffset = 0;
                    int selectionEndAbsoluteOffset = 0;

                    //selectionStartAbsoluteOffset = selected.TopPoint.AbsoluteCharOffset;
                    //selectionEndAbsoluteOffset = selected.TopPoint.AbsoluteCharOffset;

                    selected.StartOfDocument();

                    while (selected.FindPattern(startPattenText, (int)vsFindOptions.vsFindOptionsRegularExpression))
                    {
                        if (selected.BottomPoint.AbsoluteCharOffset < tmpSelectionStartAbsoluteOffset ||
                            (selected.Text == "" &&
                                 selected.BottomPoint.AbsoluteCharOffset == tmpSelectionStartAbsoluteOffset))
                        {
                            selectionStartAbsoluteOffset = selected.BottomPoint.AbsoluteCharOffset;
                        }
                        else
                        {
                            if (selectionStartAbsoluteOffset != 0)
                            {
                                selected.MoveToAbsoluteOffset(selectionStartAbsoluteOffset, false);
                            }
                            break;
                        }
                    }

                    if (selectionStartAbsoluteOffset != 0 && selected.FindPattern(endPattenText, (int)vsFindOptions.vsFindOptionsRegularExpression))
                    {
                        selectionEndAbsoluteOffset = selected.TopPoint.AbsoluteCharOffset;
                        // Restore the original selection:
                        selected.MoveToAbsoluteOffset(selectionStartAbsoluteOffset, false);
                        selected.MoveToAbsoluteOffset(selectionEndAbsoluteOffset, true);
                    }
                    else
                    {
                        selected.MoveToAbsoluteOffset(tmpSelectionStartAbsoluteOffset, false);
                        selected.MoveToAbsoluteOffset(tmpSelectionEndAbsoluteOffset, true);
                    }
                }
            }
        }
    }
}
