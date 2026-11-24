/** Query parameters of a request; `null` and `undefined` values are not sent. */
export type ApiParam = {
  [param: string]:
    | string
    | number
    | boolean
    | ReadonlyArray<string | number | boolean>
    | null
    | undefined;
};
