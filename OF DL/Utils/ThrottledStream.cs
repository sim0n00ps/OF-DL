using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Text;
using System.Threading.Tasks;

namespace OF_DL.Utils;


public class ThrottledStream : Stream

{
    private readonly Stream parent;
    private readonly int maxBytesPerSecond;
    private readonly IScheduler scheduler;
    private readonly IStopwatch stopwatch;
    private readonly bool shouldThrottle;

    private long processed;

    public ThrottledStream(Stream parent, int maxBytesPerSecond, IScheduler scheduler, bool shouldThrottle)
    {
        this.shouldThrottle = shouldThrottle;
        this.maxBytesPerSecond = maxBytesPerSecond;
        this.parent = parent;
        this.scheduler = scheduler;
        stopwatch = scheduler.StartStopwatch();
        processed = 0;
    }

    public ThrottledStream(Stream parent, int maxBytesPerSecond, bool shouldThrottle)
        : this(parent, maxBytesPerSecond, Scheduler.Immediate, shouldThrottle)
    {
    }


    protected void Throttle(int bytes)
    {
        if (!shouldThrottle) return;
        processed += bytes;
        var targetTime = TimeSpan.FromSeconds((double)processed / maxBytesPerSecond);
        var actualTime = stopwatch.Elapsed;
        var sleep = targetTime - actualTime;
        if (sleep > TimeSpan.Zero)
        {
            using var waitHandle = new AutoResetEvent(initialState: false);
            scheduler.Sleep(sleep).GetAwaiter().OnCompleted(() => waitHandle.Set());
            waitHandle.WaitOne();
        }
    }

    protected async Task ThrottleAsync(int bytes)
    {
        if (!shouldThrottle) return;
        processed += bytes;
        var targetTime = TimeSpan.FromSeconds((double)processed / maxBytesPerSecond);
        var actualTime = stopwatch.Elapsed;
        var sleep = targetTime - actualTime;

        if (sleep > TimeSpan.Zero)
        {
            await Task.Delay(sleep, CancellationToken.None).ConfigureAwait(false);
        }
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var read = await parent.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        await ThrottleAsync(read).ConfigureAwait(false);
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int bytesRead = await parent.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        await ThrottleAsync(bytesRead).ConfigureAwait(false);
        return bytesRead;
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await ThrottleAsync(count).ConfigureAwait(false);
        await parent.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await ThrottleAsync(buffer.Length).ConfigureAwait(false);
        await parent.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }


    public override bool CanRead
    {
        get { return parent.CanRead; }
    }


    public override bool CanSeek
    {
        get { return parent.CanSeek; }
    }


    public override bool CanWrite
    {
        get { return parent.CanWrite; }
    }


    public override void Flush()
    {
        parent.Flush();
    }


    public override long Length
    {
        get { return parent.Length; }
    }


    public override long Position
    {
        get
        {
            return parent.Position;
        }
        set
        {
            parent.Position = value;
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = parent.Read(buffer, offset, count);
        Throttle(read);
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return parent.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        parent.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        Throttle(count);
        parent.Write(buffer, offset, count);
    }
}
