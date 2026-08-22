using System;

/// <summary>
/// 通用倒计时计时器：Start 启动，Tick 每帧调用，归零时触发 OnTimerEnd。
/// 替代散落在各脚本中的手写倒计时逻辑（m_xxxTimer -= Time.deltaTime）。
/// </summary>
public class CountdownTimer
{
    public float TimeLeft { get; private set; }
    public bool IsRunning { get; private set; }

    public event Action OnTimerEnd;

    public void Start(float duration)
    {
        TimeLeft = duration;
        IsRunning = true;
    }

    public void Tick(float deltaTime)
    {
        if (!IsRunning) return;

        TimeLeft -= deltaTime;
        if (TimeLeft <= 0f)
        {
            IsRunning = false;
            OnTimerEnd?.Invoke();
        }
    }

    public void Stop() => IsRunning = false;
}
