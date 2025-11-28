# [Process:Close PPP Session]

# [Script:remove:enable]
/ppp active remove [find session-id="0x{{session-id}}"]

# [Script:check:enable]
/ppp active print where session-id="0x{{session-id}}"

# [Output:return]
