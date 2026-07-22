/*
 * 역할: 게임 재화를 단순 double이 아닌 규칙을 가진 불변 값 객체로 표현합니다.
 * 핵심 설계: 음수 금지, 통일된 표시와 재화 간 연산을 한곳에서 보장합니다.
 */
using System;
using Utility;

/// <summary>
/// 재화 값과 유효성·표시·연산 규칙을 캡슐화한 readonly struct입니다.
/// </summary>
public readonly struct Currency
{
    /// <summary>
    /// 외부에서 변경할 수 없는 실제 재화 값입니다.
    /// </summary>
    public readonly double Value;

    /// <summary>
    /// 잘못된 음수 재화가 시스템에 들어오는 시점에 즉시 예외를 발생시킵니다.
    /// </summary>
    public Currency(double value)
    {
        // 잘못된 재화 값은 사용 지점이 아니라 생성 시점에 즉시 차단합니다.
        if (value < 0)
        {
            throw new Exception("Currency 값은 0보다 작을 수 없습니다.");
        }
        Value = value;
    }

    /// <summary>
    /// UI와 로그에서 동일한 큰 수 표기 규칙을 사용합니다.
    /// </summary>
    public override string ToString()
    {
        return Value.ToFormattedString();
    }

    // 도메인 값끼리 일반 숫자처럼 연산할 수 있도록 연산자를 제공합니다.
    public static Currency operator +(Currency currency1, Currency currency2)
    {
        return new Currency(currency1.Value + currency2.Value);
    }
    public static Currency operator -(Currency currency1, Currency currency2)
    {
        return new Currency(currency1.Value - currency2.Value);
    }
    public static bool operator >=(Currency currency1, Currency currency2)
    {
        return currency1.Value >= currency2.Value;
    }
    public static bool operator <=(Currency currency1, Currency currency2)
    {
        return currency1.Value <= currency2.Value;
    }
    public static bool operator >(Currency currency1, Currency currency2)
    {
        return currency1.Value > currency2.Value;
    }
    public static bool operator <(Currency currency1, Currency currency2)
    {
        return currency1.Value < currency2.Value;
    }
    // 외부 계산 결과를 Currency로 받을 때 생성자 검증을 동일하게 적용합니다.
    public static implicit operator Currency(double value)
    {
        return new Currency(value);
    }

    // 저장 DTO 등 원시 수치가 필요한 경계에서만 명시적으로 변환합니다.
    public static explicit operator double(Currency currency)
    {
        return currency.Value;
    }
}
