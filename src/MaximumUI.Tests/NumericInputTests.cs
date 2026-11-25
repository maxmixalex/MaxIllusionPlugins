using System;
using System.Globalization;
using System.Reflection;
using MaximumUI;

namespace MaximumUI.Tests {
    internal static class NumericInputTests {
        internal const int TestCount = 12;
        private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
        private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

        internal static void Run() {
            DecimalSeparatorsNeverBecomeThousands();
            IncompleteNumbersDoNotPublish();
            NonFiniteAndOverflowNumbersAreRejected();
            EditsPublishOnlyNewValidValues();
            DeferredModelsPreserveTheEditingBuffer();
            EarlierAcknowledgementsPreserveNewerEdits();
            ExternalChangesRebaseCancellation();
            CommitAndEscapeHaveDistinctOutcomes();
            BecomingReadOnlyAbandonsTheBufferWithoutPublishing();
            OptionalAndReversedBoundsClampValues();
            FormattingPreservesEachNumericTypesPrecision();
            IdleSynchronizationAllocatesNothing();
        }

        private static void DecimalSeparatorsNeverBecomeThousands() {
            Number("1.5", NumericInputKind.Double, English, 1.5);
            Number("1,5", NumericInputKind.Double, German, 1.5);
            Number("1.5", NumericInputKind.Double, German, 1.5);
            Number("1.234", NumericInputKind.Double, German, 1.234);
            Number("1,234", NumericInputKind.Double, German, 1.234);
            Number("  -1,25e+2  ", NumericInputKind.Double, German, -125);
            Number(".5", NumericInputKind.Float, German, 0.5);
            Number(",5", NumericInputKind.Float, German, 0.5);
            Invalid("1,234", NumericInputKind.Double, English);
            Invalid("1,234.5", NumericInputKind.Double, English);
            Invalid("1.234,5", NumericInputKind.Double, German);
            Invalid("1 234", NumericInputKind.Double, CultureInfo.GetCultureInfo("fr-FR"));
            Invalid("1\u00a0234", NumericInputKind.Double, CultureInfo.GetCultureInfo("fr-FR"));
            Invalid("1,000", NumericInputKind.Int, English);
            Invalid("1.000", NumericInputKind.Int, German);
            Number("+123", NumericInputKind.Int, English, 123);
            CultureInfo previous = System.Threading.Thread.CurrentThread.CurrentCulture;
            try {
                System.Threading.Thread.CurrentThread.CurrentCulture = German;
                double parsed;
                Assert(NumericInputParser.Parse("2,5", NumericInputKind.Double, null, false, out parsed) == NumericParseStatus.Valid && parsed == 2.5,
                    "An omitted culture must use the actual current culture.");
            } finally { System.Threading.Thread.CurrentThread.CurrentCulture = previous; }
        }

        private static void IncompleteNumbersDoNotPublish() {
            string[] intermediate = { "", "-", "+", ".", "-.", "12.", "12e", "12E+", "12e-", "1.5e-" };
            for (int i = 0; i < intermediate.Length; i++) {
                double parsed;
                Assert(NumericInputParser.Parse(intermediate[i], NumericInputKind.Double, English, false, out parsed) == NumericParseStatus.Incomplete,
                    "An unfinished decimal/exponent must remain editable: " + intermediate[i]);
            }
            double value;
            Assert(NumericInputParser.Parse("12,", NumericInputKind.Float, German, false, out value) == NumericParseStatus.Incomplete,
                "The current culture's decimal separator also has an intermediate state.");
            Assert(NumericInputParser.Parse("12.", NumericInputKind.Double, English, true, out value) == NumericParseStatus.Valid && value == 12,
                "Enter may commit a trailing decimal point once the user finishes editing.");
            Assert(NumericInputParser.Parse("12e-", NumericInputKind.Double, English, true, out value) == NumericParseStatus.Incomplete,
                "An unfinished exponent is still invalid at commit.");
            Number("1.2e3", NumericInputKind.Double, English, 1200);
            Invalid("1e3", NumericInputKind.Int, English);
            Invalid("1.0", NumericInputKind.Int, English);
            Invalid("--1", NumericInputKind.Double, English);
        }

        private static void NonFiniteAndOverflowNumbersAreRejected() {
            Invalid("2147483648", NumericInputKind.Int, English);
            Invalid("-2147483649", NumericInputKind.Int, English);
            Invalid("3.5e38", NumericInputKind.Float, English);
            Invalid("-3.5e38", NumericInputKind.Float, English);
            Invalid("1e309", NumericInputKind.Double, English);
            Invalid("-1e309", NumericInputKind.Double, English);
            Invalid("NaN", NumericInputKind.Double, English);
            Invalid("Infinity", NumericInputKind.Float, English);
            Invalid("-Infinity", NumericInputKind.Double, English);
            Number(int.MinValue.ToString(English), NumericInputKind.Int, English, int.MinValue);
            Number(int.MaxValue.ToString(English), NumericInputKind.Int, English, int.MaxValue);
            Number(float.MaxValue.ToString("R", English), NumericInputKind.Float, English, float.MaxValue);
            Number(double.MaxValue.ToString("R", English), NumericInputKind.Double, English, double.MaxValue);
        }

        private static void EditsPublishOnlyNewValidValues() {
            NumericInputState state = State(5, NumericInputKind.Double);
            double next;
            state.BeginEdit();
            Assert(!state.Edit("-", out next) && next == 5 && state.Text == "-", "A sign alone preserves the model and the user's text.");
            Assert(state.Edit("-2", out next) && next == -2, "Complete numbers publish during editing.");
            Assert(!state.Edit("-2.", out next) && next == -2 && state.Text == "-2.", "A decimal separator does not normalize the editing buffer.");
            Assert(state.Edit("-2.5", out next) && next == -2.5, "A completed fraction publishes immediately.");
            Assert(!state.Edit("-2.5", out next), "Repeating the same value must not enqueue duplicate callbacks.");
            Assert(!state.Edit("-2..5", out next) && state.Value == -2.5, "Malformed text must not replace the last valid value.");
            Assert(!state.Commit(out next) && state.Text == "-2.5" && !state.IsEditing, "Blur restores malformed text to the last valid live value.");
        }

        private static void DeferredModelsPreserveTheEditingBuffer() {
            NumericInputState state = State(10, NumericInputKind.Double);
            double next;
            Assert(state.Edit("1", out next), "First live edit should publish.");
            Sync(state, 10, NumericInputKind.Double);
            Assert(state.Value == 1 && state.Text == "1", "The old model must not replace a pending OnGUI edit.");
            state.Edit("1e", out next);
            Sync(state, 10, NumericInputKind.Double);
            Sync(state, 1, NumericInputKind.Double);
            Assert(state.Text == "1e" && state.Value == 1, "An acknowledgement must preserve a newer incomplete exponent.");
            Assert(state.Edit("1e2", out next) && next == 100, "Completing the exponent publishes the numeric result.");
            state.Edit("1e2.", out next);
            state.Commit(out next);
            Sync(state, 1, NumericInputKind.Double);
            Assert(state.Value == 100 && state.Text == "100" && !state.IsEditing, "A committed pending value also survives old-model frames after focus leaves.");
            Sync(state, 100, NumericInputKind.Double);
            Assert(state.Text == "100", "Acknowledging a committed value must remain stable.");
        }

        private static void EarlierAcknowledgementsPreserveNewerEdits() {
            NumericInputState state = State(0, NumericInputKind.Int);
            double next;
            state.Edit("1", out next);
            state.Edit("12", out next);
            state.Edit("123", out next);
            Sync(state, 1, NumericInputKind.Int);
            Assert(state.Value == 123 && state.Text == "123", "An earlier callback acknowledgement cannot overwrite a later buffered value.");
            Sync(state, 12, NumericInputKind.Int);
            Sync(state, 123, NumericInputKind.Int);
            Assert(state.Cancel(out next) && next == 0, "Acknowledging live edits must preserve the original Escape baseline.");
            Sync(state, 123, NumericInputKind.Int);
            Assert(state.Value == 0 && state.Text == "0", "A pending cancellation must survive an old-model frame.");
            Sync(state, 0, NumericInputKind.Int);
        }

        private static void ExternalChangesRebaseCancellation() {
            NumericInputState state = State(1, NumericInputKind.Double);
            double next;
            state.Edit("2", out next);
            Sync(state, 8, NumericInputKind.Double);
            Assert(state.Text == "8" && state.Value == 8 && state.IsEditing, "A real external model change must replace the buffer without losing GUI focus state.");
            state.Edit("9", out next);
            Assert(state.Cancel(out next) && next == 8, "Escape must not overwrite a newer external model with the pre-edit value.");
            Sync(state, 9, NumericInputKind.Double);
            Sync(state, 8, NumericInputKind.Double);
            Sync(state, 88, NumericInputKind.Double);
            Assert(state.Text == "88" && state.Value == 88, "Idle fields must synchronize model changes as well.");
            state.Edit("-", out next);
            Sync(state, 7, NumericInputKind.Double);
            Assert(state.Text == "7" && !state.Cancel(out next) && next == 7, "An external update also replaces an invalid buffer and its cancellation baseline.");
        }

        private static void CommitAndEscapeHaveDistinctOutcomes() {
            NumericInputState state = State(5, NumericInputKind.Double);
            double next;
            Assert(!state.Edit("12.", out next) && state.Value == 5, "An unfinished decimal must not publish early.");
            Assert(state.Commit(out next) && next == 12 && state.Text == "12" && !state.IsEditing, "Enter finalizes a valid trailing decimal.");
            Sync(state, 12, NumericInputKind.Double);
            state.Edit("13", out next);
            state.Edit("-", out next);
            Assert(state.Cancel(out next) && next == 12 && state.Text == "12", "Escape restores the focus-start value, including live changes already sent.");
            Assert(!state.Cancel(out next), "A second Escape must not emit another rollback.");
            state.Edit("-", out next);
            Assert(!state.Commit(out next) && next == 12 && state.Text == "12", "An invalid blur restores the latest valid value without a callback.");
            NumericInputState cancelled = State(5, NumericInputKind.Double);
            cancelled.Edit("6", out next);
            cancelled.Cancel(out next);
            Sync(cancelled, 5, NumericInputKind.Double);
            Sync(cancelled, 6, NumericInputKind.Double);
            Assert(cancelled.Value == 6 && cancelled.Text == "6", "After batched edit/cancel callbacks return to the original model, a later real external update must not look like an old acknowledgement.");
        }

        private static void OptionalAndReversedBoundsClampValues() {
            NumericInputState state = new NumericInputState();
            state.Synchronize(5, NumericInputKind.Int, true, 0, true, 10, English);
            double next;
            Assert(state.Edit("20", out next) && next == 10 && state.Text == "20", "Bounds clamp the published number while preserving the editable text.");
            Assert(!state.Commit(out next) && state.Text == "10", "Commit canonicalizes an already-clamped value without duplicate publication.");
            Assert(!state.Edit("2147483648", out next), "Type overflow must be rejected before range clamping.");
            state.Commit(out next);
            state.Synchronize(10, NumericInputKind.Int, true, 10, true, 0, English);
            Assert(state.Edit("-3", out next) && next == 0, "Reversed finite bounds are normalized by swapping them.");
            state.Synchronize(0, NumericInputKind.Int, false, 999, true, 5, English);
            Assert(state.Edit("8", out next) && next == 5, "A disabled minimum must not affect upper-only clamping.");
            state.Synchronize(5, NumericInputKind.Int, true, 0, false, -999, English);
            Assert(state.Edit("-8", out next) && next == 0, "A disabled maximum must not affect lower-only clamping.");
            NumericInputState single = new NumericInputState();
            single.Synchronize(0.15f, NumericInputKind.Float, true, 0.1f, true, 0.2f, English);
            Assert(single.Edit("0.7", out next) && next == (double)0.2f, "Single-precision bounds must publish single-precision values.");
            NumericInputState wide = new NumericInputState();
            wide.Synchronize(0, NumericInputKind.Double, true, 10, true, -10, English);
            Assert(wide.Edit("30", out next) && next == 10 && wide.Edit("-30", out next) && next == -10, "Reversed double bounds must clamp both ends.");
            wide.Synchronize(-10, NumericInputKind.Double, true, double.NaN, true, double.PositiveInfinity, English);
            Assert(wide.Edit("50", out next) && next == 50, "Non-finite bounds cannot introduce a non-finite published value.");
        }

        private static void BecomingReadOnlyAbandonsTheBufferWithoutPublishing() {
            NumericInputState state = State(5, NumericInputKind.Double);
            double next;
            Assert(!state.Edit("12.", out next), "A trailing decimal must still be pending when read-only begins.");
            state.Abandon(5);
            Assert(!state.IsEditing && state.Value == 5 && state.Text == "5" && !state.Cancel(out next) && !state.Commit(out next),
                "Becoming read-only must discard unfinished text without committing it or generating a rollback.");

            state.Edit("7", out next);
            Sync(state, 7, NumericInputKind.Double);
            state.Edit("8.", out next);
            state.Abandon(7);
            Assert(state.Value == 7 && state.Text == "7" && !state.Cancel(out next),
                "Read-only must retain already-accepted model changes instead of restoring the old focus-start value.");

            state.Edit("9", out next);
            state.Abandon(7);
            Assert(state.Value == 7 && state.Text == "7" && !state.IsEditing,
                "Read-only must show the actual model, not a predicted value awaiting a deferred callback.");
            Sync(state, 9, NumericInputKind.Double);
            Assert(state.Value == 9 && state.Text == "9", "A callback accepted later becomes an ordinary external model update.");

            state.Synchronize(1.5f, NumericInputKind.Float, false, 0, false, 0, German);
            state.Edit("-", out next);
            state.Abandon(2.5f);
            Assert(state.Value == 2.5f && state.Text == "2,5" && !state.IsEditing,
                "Abandon must preserve the configured numeric type and culture when adopting the real model.");
        }

        private static void FormattingPreservesEachNumericTypesPrecision() {
            double[] values = { 0.12345678901234567, double.Epsilon, 1e300, -1e-200 };
            for (int i = 0; i < values.Length; i++) {
                NumericInputState state = State(values[i], NumericInputKind.Double);
                Number(state.Text, NumericInputKind.Double, English, values[i]);
                double next;
                state.BeginEdit();
                Assert(!state.Commit(out next) && next.Equals(values[i]), "Focusing and committing must not round or change double values.");
            }
            float single = 0.12345679f;
            NumericInputState floats = State(single, NumericInputKind.Float);
            Number(floats.Text, NumericInputKind.Float, English, single);
            double result;
            Assert(floats.Edit("0.000000001", out result) && result == (double)0.000000001f,
                "Small real changes must not disappear behind an arbitrary approximate-equality threshold.");
        }

        private static void IdleSynchronizationAllocatesNothing() {
            MethodInfo method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes);
            if (method == null) throw new InvalidOperationException("Numeric allocation regression requires the documented .NET 9 test runner.");
            Func<long> allocated = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
            NumericInputState state = State(42, NumericInputKind.Double);
            for (int i = 0; i < 100; i++) Sync(state, 42, NumericInputKind.Double);
            allocated();
            long before = allocated();
            for (int i = 0; i < 10000; i++) Sync(state, 42, NumericInputKind.Double);
            long bytes = allocated() - before;
            Assert(bytes == 0 && state.Text == "42", "Unchanged numeric fields must not allocate or reformat per GUI event; actual " + bytes + ".");
        }

        private static NumericInputState State(double value, NumericInputKind kind) {
            NumericInputState state = new NumericInputState();
            Sync(state, value, kind);
            return state;
        }

        private static void Sync(NumericInputState state, double value, NumericInputKind kind) {
            state.Synchronize(value, kind, false, 0, false, 0, English);
        }

        private static void Number(string text, NumericInputKind kind, CultureInfo culture, double expected) {
            double value;
            Assert(NumericInputParser.Parse(text, kind, culture, false, out value) == NumericParseStatus.Valid && value.Equals(expected),
                "Expected a finite exact numeric parse for " + text + " in " + culture.Name + ".");
        }

        private static void Invalid(string text, NumericInputKind kind, CultureInfo culture) {
            double value;
            Assert(NumericInputParser.Parse(text, kind, culture, true, out value) == NumericParseStatus.Invalid && value == 0,
                "Expected invalid numeric data without a numeric result: " + text + ".");
        }

        private static void Assert(bool condition, string message) {
            if (!condition) throw new Exception(message);
        }
    }
}
