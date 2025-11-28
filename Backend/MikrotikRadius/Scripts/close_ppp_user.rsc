# [Process:Close PPP Session]

# [Script:remove:enable]
/ppp active remove [find name={{username}}]

# [Script:check:enable]
/ppp active print where name={{username}}

# [Output:return]
