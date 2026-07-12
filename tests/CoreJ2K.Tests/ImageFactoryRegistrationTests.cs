using System;
using System.Threading;
using System.Threading.Tasks;
using CoreJ2K.j2k.image;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Regression tests for ImageFactory registration semantics: idempotent Register,
    /// exact-match-then-assignable resolution, propagation of creator exceptions, and
    /// resolution staying reliable while another thread registers creators (previously a
    /// Register racing an in-flight decode made New return null via a swallowed
    /// "Collection was modified" exception).
    /// All marker types are private to this fixture so the shared ImageFactory state
    /// cannot interfere with other test classes running in parallel.
    /// </summary>
    public class ImageFactoryRegistrationTests
    {
        private class ReplaceMarker { }
        private class ExactBaseMarker { }
        private sealed class ExactDerivedMarker : ExactBaseMarker { }
        private class FallbackBaseMarker { }
        private sealed class FallbackDerivedMarker : FallbackBaseMarker { }
        private sealed class ThrowMarker { }
        private sealed class RaceMarker { }
        private sealed class ChurnMarker { }

        /// <summary>IImage stub whose payload identifies the creator that produced it.</summary>
        private sealed class StubImage : IImage
        {
            public StubImage(object payload) { Payload = payload; }
            public object Payload { get; }
            public T As<T>() => (T)Payload;
        }

        private sealed class StubCreator<TBase> : ImageCreator<TBase>
        {
            public override IImage Create(int width, int height, int numComponents, byte[] bytes) =>
                new StubImage(this);

            public override BlkImgDataSrc ToPortableImageSource(object imageObject) =>
                throw new NotSupportedException();
        }

        private sealed class ThrowingCreator : ImageCreator<ThrowMarker>
        {
            public override IImage Create(int width, int height, int numComponents, byte[] bytes) =>
                throw new InvalidOperationException("creator boom");

            public override BlkImgDataSrc ToPortableImageSource(object imageObject) =>
                throw new NotSupportedException();
        }

        [Fact]
        public void Register_SameImageType_ReplacesInsteadOfDuplicating()
        {
            var first = new StubCreator<ReplaceMarker>();
            var second = new StubCreator<ReplaceMarker>();
            ImageFactory.Register(first);
            ImageFactory.Register(second);

            // Before the fix a duplicate registration made Single() throw and every
            // New<T> for that type returned null.
            var img = ImageFactory.New<ReplaceMarker>(1, 1, 1, new byte[1]);
            Assert.NotNull(img);
            Assert.Same(second, ((StubImage)img).Payload);
        }

        [Fact]
        public void New_PrefersExactImageTypeMatchOverAssignable()
        {
            var baseCreator = new StubCreator<ExactBaseMarker>();
            var derivedCreator = new StubCreator<ExactDerivedMarker>();
            ImageFactory.Register(baseCreator);
            ImageFactory.Register(derivedCreator);

            var forDerived = ImageFactory.New<ExactDerivedMarker>(1, 1, 1, new byte[1]);
            Assert.NotNull(forDerived);
            Assert.Same(derivedCreator, ((StubImage)forDerived).Payload);

            var forBase = ImageFactory.New<ExactBaseMarker>(1, 1, 1, new byte[1]);
            Assert.NotNull(forBase);
            Assert.Same(baseCreator, ((StubImage)forBase).Payload);
        }

        [Fact]
        public void New_FallsBackToAssignableCreator()
        {
            var baseCreator = new StubCreator<FallbackBaseMarker>();
            ImageFactory.Register(baseCreator);

            var img = ImageFactory.New<FallbackDerivedMarker>(1, 1, 1, new byte[1]);
            Assert.NotNull(img);
            Assert.Same(baseCreator, ((StubImage)img).Payload);
        }

        [Fact]
        public void New_UnregisteredType_ReturnsNull()
        {
            Assert.Null(ImageFactory.New<string>(1, 1, 1, new byte[1]));
        }

        [Fact]
        public void New_CreatorExceptionPropagates()
        {
            ImageFactory.Register(new ThrowingCreator());

            // Before the fix creator exceptions were swallowed and surfaced as the
            // misleading "No image creator registered" further up the stack.
            var ex = Assert.Throws<InvalidOperationException>(
                () => ImageFactory.New<ThrowMarker>(1, 1, 1, new byte[1]));
            Assert.Equal("creator boom", ex.Message);
        }

        [Fact]
        public async Task New_SucceedsWhileAnotherThreadRegisters()
        {
            ImageFactory.Register(new StubCreator<RaceMarker>());

            // The registrar is iteration-bounded (not stop-flag-only) so that an
            // implementation without idempotent Register cannot grow the creator list
            // unboundedly and turn this test into a hang instead of a failure. The start
            // gate and the spin between registrations keep the Register calls overlapping
            // the New calls instead of completing before the decoders spin up.
            var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var stop = 0;
            var registrar = Task.Run(async () =>
            {
                await start.Task;
                for (var i = 0; i < 2_000 && Volatile.Read(ref stop) == 0; i++)
                {
                    ImageFactory.Register(new StubCreator<ChurnMarker>());
                    Thread.SpinWait(200);
                }
            });

            var nullCount = 0;
            const int decodersCount = 4;
            const int iterations = 20_000;
            var decoders = new Task[decodersCount];
            for (var t = 0; t < decodersCount; t++)
            {
                decoders[t] = Task.Run(async () =>
                {
                    await start.Task;
                    for (var i = 0; i < iterations; i++)
                    {
                        if (ImageFactory.New<RaceMarker>(1, 1, 1, new byte[1]) == null)
                            Interlocked.Increment(ref nullCount);
                    }
                });
            }

            start.SetResult(true);
            await Task.WhenAll(decoders);
            Volatile.Write(ref stop, 1);
            await registrar;

            // Before the fix an unsynchronized Register racing New made ~10% of
            // resolutions fail with a swallowed "Collection was modified" exception.
            Assert.Equal(0, nullCount);
        }
    }
}
