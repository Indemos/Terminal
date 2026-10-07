using Core.Models;
using System;

namespace Core.Indicators;

public class AdxIndicator
{
  protected int count;

  // Initial SMA seeds
  protected double trSum;
  protected double plusDmSum;
  protected double minusDmSum;
  protected double dxSum;

  // Last closed Wilder values
  protected double previousAtr;
  protected double previousPlusDm;
  protected double previousMinusDm;
  protected double previousAdx;

  // Previous closed bar
  protected double? previousHigh;
  protected double? previousLow;
  protected double? previousClose;

  // Current forming bar
  protected long currentTime;
  protected double currentHigh;
  protected double currentLow;
  protected double currentClose;

  // Current bar contributions, used for replacement
  protected double currentTr;
  protected double currentPlusDm;
  protected double currentMinusDm;
  protected double currentDx;
  protected bool setup;

  public virtual int Period { get; set; } = 15;

  public virtual double Value { get; protected set; }

  public virtual double PlusDi { get; protected set; }

  public virtual double MinusDi { get; protected set; }

  public virtual double Update(long stamp, Price price)
  {
    if (price.Bar is null ||
        price.Bar.Low is not double L ||
        price.Bar.High is not double H ||
        price.Bar.Close is not double C)
    {
      return Value;
    }

    if (setup)
    {
      if (currentTime == stamp)
      {
        return UpdateBar(H, L, C, true);
      }

      // Freeze current bar as the previous closed bar.
      previousHigh = currentHigh;
      previousLow = currentLow;
      previousClose = currentClose;

      // Freeze Wilder state.
      if (count >= Period)
      {
        previousAtr = Atr;
        previousPlusDm = PlusDm;
        previousMinusDm = MinusDm;
      }

      if (count >= Period * 2 - 1)
      {
        previousAdx = Value;
      }
    }

    Value = UpdateBar(H, L, C, false);
    currentTime = stamp;
    setup = true;

    return Value;
  }

  protected double Atr => count <= Period ?
    trSum / count :
    (previousAtr * (Period - 1) + currentTr) / Period;

  protected double PlusDm => count <= Period ?
    plusDmSum / count :
    (previousPlusDm * (Period - 1) + currentPlusDm) / Period;

  protected double MinusDm => count <= Period ?
    minusDmSum / count :
    (previousMinusDm * (Period - 1) + currentMinusDm) / Period;

  protected double UpdateBar(
    double H,
    double L,
    double C,
    bool replace)
  {
    var tr = previousClose is null
      ? H - L
      : Math.Max(
          H - L,
          Math.Max(
            Math.Abs(H - previousClose.Value),
            Math.Abs(L - previousClose.Value)));

    var plusDm = 0.0;
    var minusDm = 0.0;

    if (previousHigh is double PH && previousLow is double PL)
    {
      var up = H - PH;
      var down = PL - L;

      if (up > down && up > 0) plusDm = up;
      if (down > up && down > 0) minusDm = down;
    }

    if (replace is false)
    {
      count++;
      trSum += tr;
      plusDmSum += plusDm;
      minusDmSum += minusDm;
    }
    else
    {
      trSum += tr - currentTr;
      plusDmSum += plusDm - currentPlusDm;
      minusDmSum += minusDm - currentMinusDm;
    }

    currentTr = tr;
    currentPlusDm = plusDm;
    currentMinusDm = minusDm;

    var atr = Atr;
    var pdi = atr > 0 ? 100.0 * PlusDm / atr : 0;
    var mdi = atr > 0 ? 100.0 * MinusDm / atr : 0;

    PlusDi = pdi;
    MinusDi = mdi;

    var diSum = pdi + mdi;
    var dx = diSum > 0 ? 100.0 * Math.Abs(pdi - mdi) / diSum : 0;

    // First Period DX values seed ADX.
    if (count >= Period)
    {
      if (count < Period * 2)
      {
        dxSum += replace ? dx - currentDx : dx;
        Value = dxSum / (count - Period + 1);
      }
      else
      {
        Value = (previousAdx * (Period - 1) + dx) / Period;
      }
    }

    currentDx = dx;
    currentHigh = H;
    currentLow = L;
    currentClose = C;

    return Value;
  }
}
