import Tip from '../help/Tip'

/** Marks a company that is in the KMI-30: the 30 largest, most traded Shariah-compliant companies. */
export default function Kmi30Badge() {
  return (
    <Tip text="In the KMI-30: the 30 largest, most traded Shariah-compliant companies on the Pakistan Stock Exchange.">
      <span style={{ display: 'inline-block', marginLeft: '6px', padding: '1px 6px', borderRadius: '6px', fontSize: '11px', fontWeight: 700, color: '#155e75', backgroundColor: '#e3f0f4', verticalAlign: 'middle' }}>
        KMI-30
      </span>
    </Tip>
  )
}
